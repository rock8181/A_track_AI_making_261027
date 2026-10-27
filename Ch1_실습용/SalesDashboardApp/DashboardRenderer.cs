using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Linq;

namespace SalesDash
{
    // 화면과 PDF가 같이 쓰는 대시보드 그리기 로직. 세로로 쌓이는 "블록" 목록을 만든다.
    public class Block
    {
        public int H;
        public Action<Graphics, int> Draw; // (g, 블록 상단 y)
    }

    public class Agg
    {
        public string Key;
        public long Sum, Cnt, Qty;
    }

    public class DashboardRenderer
    {
        const int M = 16, G = 14;
        const int OrderRowsPerBlock = 16, PrintOrderRows = 13;

        public string Title = "매출 대시보드";
        public string Source = "";
        public List<Order> Items = new List<Order>();   // 필터가 적용된 주문
        public List<string> Months = new List<string>(); // 전체 데이터의 월 목록 (YYYY-MM), 필터와 무관
        public bool HasData;

        static string Num(long n) { return n.ToString("N0", CultureInfo.InvariantCulture); }

        public static List<Agg> Group(IEnumerable<Order> items, Func<Order, string> key)
        {
            var m = new Dictionary<string, Agg>();
            foreach (var o in items)
            {
                string k = key(o);
                Agg a;
                if (!m.TryGetValue(k, out a)) { a = new Agg(); a.Key = k; m[k] = a; }
                a.Sum += o.Amount; a.Cnt++; a.Qty += o.Qty;
            }
            return m.Values.OrderByDescending(x => x.Sum).ThenBy(x => x.Key, StringComparer.Ordinal).ToList();
        }

        // 상위 max개 + 나머지는 "기타 N개"로 합친다 (패널 높이를 제한하기 위함)
        static List<Agg> TopN(List<Agg> list, int max)
        {
            if (list.Count <= max) return list;
            var r = list.Take(max - 1).ToList();
            var rest = list.Skip(max - 1).ToList();
            r.Add(new Agg { Key = "기타 " + rest.Count + "개", Sum = rest.Sum(x => x.Sum), Cnt = rest.Sum(x => x.Cnt), Qty = rest.Sum(x => x.Qty) });
            return r;
        }

        static List<KeyValuePair<string, long>> Pairs(List<Agg> l)
        {
            return l.Select(a => new KeyValuePair<string, long>(a.Key, a.Sum)).ToList();
        }

        string PeriodLabel()
        {
            if (Months.Count == 0) return "";
            string a = Months[0], b = Months[Months.Count - 1];
            string ya = a.Substring(0, 4);
            if (ya == b.Substring(0, 4))
            {
                int ma = int.Parse(a.Substring(5, 2)), mb = int.Parse(b.Substring(5, 2));
                if (ma == 1 && mb == 12) return ya + " 연간";
                if (ma == 1 && mb <= 6) return ya + " 상반기";
                if (ma >= 7 && mb == 12) return ya + " 하반기";
            }
            return a + " ~ " + b;
        }

        static void Text(Graphics g, string s, Font f, Color c, RectangleF r, StringAlignment h, StringAlignment v)
        {
            using (var br = new SolidBrush(c))
            using (var sf = new StringFormat(StringFormatFlags.NoWrap))
            {
                sf.Alignment = h; sf.LineAlignment = v; sf.Trimming = StringTrimming.EllipsisCharacter;
                g.DrawString(s, f, br, r, sf);
            }
        }

        static void PanelBox(Graphics g, Rectangle r, string title, bool top, bool bottom)
        {
            using (var br = new SolidBrush(Theme.Panel)) g.FillRectangle(br, r);
            using (var p = new Pen(Theme.Line))
            {
                g.DrawLine(p, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(p, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                if (top) g.DrawLine(p, r.Left, r.Top, r.Right - 1, r.Top);
                if (bottom) g.DrawLine(p, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
            }
            if (title != null)
                using (var f = Theme.Font(13, true))
                    Text(g, title, f, Theme.Cyan, new RectangleF(r.Left + 14, r.Top + 10, r.Width - 28, 20), StringAlignment.Near, StringAlignment.Near);
        }

        public List<Block> Build(int W) { return Build(W, false); }

        public List<Block> Build(int W, bool print)
        {
            var blocks = new List<Block>();
            int cw = W - 2 * M;

            blocks.Add(new Block { H = 78 + G, Draw = (g, y) => DrawHeader(g, y, cw) });

            if (!HasData)
            {
                blocks.Add(new Block { H = 180, Draw = (g, y) => {
                    var r = new Rectangle(M, y, cw, 160);
                    PanelBox(g, r, null, true, true);
                    using (var f = Theme.Font(20, true))
                        Text(g, "엑셀 파일을 불러오세요", f, Theme.Yellow, new RectangleF(r.Left, r.Top + 34, r.Width, 36), StringAlignment.Center, StringAlignment.Center);
                    using (var f = Theme.Font(14, false))
                        Text(g, "상단의 [엑셀 불러오기] 또는 파일을 이 창에 끌어다 놓으세요.\n첫 행 컬럼: 주문번호 · 주문일자 · 지역 · 담당자 · 채널 · 제품 · 단가 · 수량 · 매출액",
                            f, Theme.Muted, new RectangleF(r.Left, r.Top + 76, r.Width, 60), StringAlignment.Center, StringAlignment.Near);
                } });
                return blocks;
            }

            var items = Items;
            long total = items.Sum(o => o.Amount), qty = items.Sum(o => o.Qty);
            long cnt = items.Count, avg = cnt > 0 ? (long)Math.Round((double)total / cnt) : 0;

            // KPI
            blocks.Add(new Block { H = 92 + G, Draw = (g, y) => {
                int tw = (cw - 3 * G) / 4;
                string[] lab = { "총매출", "주문 건수", "평균 주문금액(객단가)", "총 판매수량" };
                string[] val = { Widgets.Won(total), Num(cnt) + "건", Widgets.Won(avg), Num(qty) + "개" };
                Color[] col = { Theme.Yellow, Theme.Cyan, Theme.Cyan, Theme.Yellow };
                for (int i = 0; i < 4; i++)
                    Widgets.DrawKpiTile(g, new Rectangle(M + i * (tw + G), y, tw, 92), lab[i], val[i], col[i]);
            } });

            // 월별 | 채널
            var chan = Group(items, o => o.Channel);
            int lw = (cw - G) * 2 / 3, rw = cw - G - lw;
            blocks.Add(new Block { H = 280 + G, Draw = (g, y) => {
                var a = new Rectangle(M, y, lw, 280);
                PanelBox(g, a, "월별 매출 추이", true, true);
                var sums = new long[Months.Count];
                foreach (var o in items)
                {
                    int i = Months.IndexOf(o.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture));
                    if (i >= 0) sums[i] += o.Amount;
                }
                bool multi = Months.Count > 0 && Months[0].Substring(0, 4) != Months[Months.Count - 1].Substring(0, 4);
                var labels = Months.Select(m => multi ? m.Substring(2, 2) + "." + m.Substring(5, 2) : int.Parse(m.Substring(5, 2)) + "월").ToArray();
                Widgets.DrawMonthBars(g, new Rectangle(a.Left + 12, a.Top + 36, a.Width - 24, a.Height - 48), labels, sums);
                var b = new Rectangle(M + lw + G, y, rw, 280);
                PanelBox(g, b, "채널별 매출", true, true);
                Widgets.DrawDonut(g, new Rectangle(b.Left + 12, b.Top + 36, b.Width - 24, b.Height - 48), Pairs(chan), total);
            } });

            // 지역 | 제품
            var reg = TopN(Group(items, o => o.Region), 10);
            var prod = TopN(Group(items, o => o.Product), 10);
            int n2 = Math.Max(1, Math.Max(reg.Count, prod.Count));
            int h2 = 48 + n2 * 34 + 12;
            int hw = (cw - G) / 2;
            blocks.Add(new Block { H = h2 + G, Draw = (g, y) => {
                var a = new Rectangle(M, y, hw, h2);
                PanelBox(g, a, "지역별 매출", true, true);
                Widgets.DrawHBars(g, new Rectangle(a.Left + 12, a.Top + 38, a.Width - 24, a.Height - 48), Pairs(reg), total);
                var b = new Rectangle(M + hw + G, y, cw - hw - G, h2);
                PanelBox(g, b, "제품별 매출", true, true);
                Widgets.DrawHBars(g, new Rectangle(b.Left + 12, b.Top + 38, b.Width - 24, b.Height - 48), Pairs(prod), total);
            } });

            // 담당자 표
            var rep = TopN(Group(items, o => o.Rep), 10);
            int h3 = 48 + 28 + rep.Count * 30 + 12;
            blocks.Add(new Block { H = h3 + G, Draw = (g, y) => DrawRepTable(g, new Rectangle(M, y, cw, h3), rep, total) });

            // 주문 내역 (블록 여러 개로 나눠 PDF 페이지 경계에서 행이 잘리지 않게 한다)
            var orders = items.OrderByDescending(o => o.Date).ThenByDescending(o => o.Amount).ToList();
            // PDF용은 페이지당 2블록이 들어가도록 13행씩 나누고 블록마다 머리글을 반복한다
            int per = print ? PrintOrderRows : OrderRowsPerBlock;
            for (int s = 0; s < orders.Count; s += per)
            {
                int start = s, take = Math.Min(per, orders.Count - s);
                bool first = s == 0, last = s + take >= orders.Count, head = first || print;
                int h = (first ? 48 : 6) + (head ? 28 : 0) + take * 24 + (last ? 12 : 0);
                blocks.Add(new Block { H = h + (last ? G : 0), Draw = (g, y) =>
                    DrawOrders(g, new Rectangle(M, y, cw, h), orders.GetRange(start, take), first, head, last, print, orders.Count) });
            }
            return blocks;
        }

        void DrawHeader(Graphics g, int y, int cw)
        {
            var pts = new[] { new Point(M, y), new Point(M + cw, y), new Point(M + cw - 28, y + 78), new Point(M, y + 78) };
            using (var br = new LinearGradientBrush(new Rectangle(M, y, cw, 78), Color.FromArgb(46, 25, 227, 255), Theme.Bg, LinearGradientMode.Horizontal))
                g.FillPolygon(br, pts);
            using (var p = new Pen(Theme.Line)) g.DrawPolygon(p, pts);
            using (var br = new SolidBrush(Theme.Yellow)) g.FillRectangle(br, M, y, 4, 78);
            string per = PeriodLabel();
            using (var f = Theme.Font(24, true))
                Text(g, (per.Length > 0 ? per + " " : "") + Title, f, Theme.Text, new RectangleF(M + 18, y + 8, cw - 60, 34), StringAlignment.Near, StringAlignment.Center);
            string sub = HasData
                ? Num(Items.Count) + "건 / " + Widgets.Won(Items.Sum(o => o.Amount)) + (Months.Count > 0 ? "  ·  기간 " + Months[0] + " ~ " + Months[Months.Count - 1] : "") + (Source.Length > 0 ? "  ·  " + Source : "")
                : "엑셀 파일을 불러오면 자동으로 채워집니다";
            using (var f = Theme.Font(13, false))
                Text(g, sub, f, Theme.Muted, new RectangleF(M + 18, y + 44, cw - 60, 24), StringAlignment.Near, StringAlignment.Center);
        }

        void DrawRepTable(Graphics g, Rectangle r, List<Agg> rep, long total)
        {
            PanelBox(g, r, "담당자별 실적", true, true);
            if (rep.Count == 0) return;
            int x0 = r.Left + 14, w = r.Width - 28;
            int[] cx = { 0, (int)(w * 0.20), (int)(w * 0.32), (int)(w * 0.44), (int)(w * 0.62), (int)(w * 0.72) };
            int[] cwid = { cx[1], cx[2] - cx[1], cx[3] - cx[2], cx[4] - cx[3], cx[5] - cx[4], w - cx[5] };
            string[] hd = { "담당자", "주문수", "판매수량", "매출", "비중", "" };
            int y = r.Top + 44;
            using (var f = Theme.Font(12, true))
                for (int i = 0; i < hd.Length; i++)
                    Text(g, hd[i], f, Theme.Muted, new RectangleF(x0 + cx[i], y, cwid[i] - 8, 24), i == 0 ? StringAlignment.Near : StringAlignment.Far, StringAlignment.Center);
            y += 28;
            long max = rep.Max(a => a.Sum);
            using (var f = Theme.Font(13, false))
            using (var fb = Theme.Font(13, true))
                for (int i = 0; i < rep.Count; i++)
                {
                    var a = rep[i];
                    Color c = i == 0 ? Theme.Yellow : Theme.Text;
                    Font ff = i == 0 ? fb : f;
                    string[] v = { a.Key, Num(a.Cnt) + "건", Num(a.Qty) + "개", Widgets.Won(a.Sum), Widgets.Pct(a.Sum, total) };
                    for (int k = 0; k < 5; k++)
                        Text(g, v[k], ff, c, new RectangleF(x0 + cx[k], y, cwid[k] - 8, 30), k == 0 ? StringAlignment.Near : StringAlignment.Far, StringAlignment.Center);
                    int bw = (int)((cwid[5] - 16) * (max > 0 ? (double)a.Sum / max : 0));
                    using (var br = new SolidBrush(Color.FromArgb(24, 255, 255, 255))) g.FillRectangle(br, x0 + cx[5] + 8, y + 11, cwid[5] - 16, 8);
                    if (bw > 0)
                        using (var br = new SolidBrush(i == 0 ? Theme.Yellow : Theme.Cyan)) g.FillRectangle(br, x0 + cx[5] + 8, y + 11, bw, 8);
                    using (var p = new Pen(Color.FromArgb(40, 25, 227, 255))) g.DrawLine(p, x0, y + 29, x0 + w, y + 29);
                    y += 30;
                }
        }

        void DrawOrders(Graphics g, Rectangle r, List<Order> rows, bool first, bool head, bool last, bool print, int totalCount)
        {
            PanelBox(g, r, first ? "주문 내역 (" + Num(totalCount) + "건)" : null, head, last || (head && !first) || print);
            int x0 = r.Left + 14, w = r.Width - 28;
            double[] frac = { 0, 0.12, 0.24, 0.34, 0.46, 0.57, 0.70, 0.78 };
            int[] cx = frac.Select(f => (int)(w * f)).ToArray();
            int[] cwid = new int[8];
            for (int i = 0; i < 8; i++) cwid[i] = (i < 7 ? cx[i + 1] : w) - cx[i];
            int y = r.Top + 6;
            if (head)
            {
                y = r.Top + (first ? 44 : 6);
                string[] hd = { "주문번호", "일자", "지역", "담당자", "채널", "제품", "수량", "매출액" };
                using (var f = Theme.Font(12, true))
                    for (int i = 0; i < 8; i++)
                        Text(g, hd[i], f, Theme.Muted, new RectangleF(x0 + cx[i], y, cwid[i] - 8, 24), i >= 6 ? StringAlignment.Far : StringAlignment.Near, StringAlignment.Center);
                y += 28;
            }
            using (var f = Theme.Font(12, false))
                foreach (var o in rows)
                {
                    string[] v = { o.Id, o.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), o.Region, o.Rep, o.Channel, o.Product, Num(o.Qty), Widgets.Won(o.Amount) };
                    for (int i = 0; i < 8; i++)
                        Text(g, v[i], f, Theme.Text, new RectangleF(x0 + cx[i], y, cwid[i] - 8, 24), i >= 6 ? StringAlignment.Far : StringAlignment.Near, StringAlignment.Center);
                    using (var p = new Pen(Color.FromArgb(30, 25, 227, 255))) g.DrawLine(p, x0, y + 23, x0 + w, y + 23);
                    y += 24;
                }
        }

        public static void Setup(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static int TotalHeight(List<Block> blocks)
        {
            int h = 0;
            foreach (var b in blocks) h += b.H;
            return h;
        }

        // 화면용: 클립 영역과 겹치는 블록만 그린다
        public static void DrawAll(Graphics g, List<Block> blocks, Rectangle clip)
        {
            Setup(g);
            int y = 0;
            foreach (var b in blocks)
            {
                if (y + b.H >= clip.Top && y <= clip.Bottom) b.Draw(g, y);
                y += b.H;
            }
        }

        // PDF용: 가로 A4 비율 페이지로 블록을 채워 넣는다 (블록은 쪼개지 않음). scale = 해상도 배율
        public List<Bitmap> RenderPages(int W, float scale)
        {
            var blocks = Build(W, true);
            int pageH = (int)Math.Round(W * 595.0 / 842.0), foot = 26, usable = pageH - foot - 8;
            var groups = new List<List<Block>>();
            var cur = new List<Block>(); int used = 0;
            foreach (var b in blocks)
            {
                if (used + b.H > usable && cur.Count > 0) { groups.Add(cur); cur = new List<Block>(); used = 0; }
                cur.Add(b); used += b.H;
            }
            if (cur.Count > 0) groups.Add(cur);

            var pages = new List<Bitmap>();
            for (int i = 0; i < groups.Count; i++)
            {
                var bmp = new Bitmap((int)(W * scale), (int)(pageH * scale));
                bmp.SetResolution(96, 96);
                using (var g = Graphics.FromImage(bmp))
                {
                    g.Clear(Theme.Bg);
                    g.ScaleTransform(scale, scale);
                    Setup(g);
                    int y = 8;
                    foreach (var b in groups[i]) { b.Draw(g, y); y += b.H; }
                    using (var f = Theme.Font(11, false))
                        Text(g, Title + "  ·  " + (i + 1) + " / " + groups.Count, f, Theme.Muted, new RectangleF(M, pageH - foot, W - 2 * M, 20), StringAlignment.Far, StringAlignment.Center);
                }
                pages.Add(bmp);
            }
            return pages;
        }
    }
}
