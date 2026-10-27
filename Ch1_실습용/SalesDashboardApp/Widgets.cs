using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;

namespace SalesDash
{
    // GDI+ 그리기 전용 위젯 모음. 컨트롤이 아니라 Graphics에 직접 그린다.
    // Brush/Pen/Font/StringFormat은 전부 using으로 즉시 해제한다 (PDF용으로 여러 번 호출됨).
    public static class Widgets
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------- 포맷 ----------

        // 334570000 -> "334,570,000원"
        public static string Won(long n)
        {
            return n.ToString("#,##0", Inv) + "원";
        }

        // 1억 이상: "3.35억" (소수 최대 2자리, 끝 0 제거)
        // 1만 이상: "1,200만"
        // 그 미만: "9,999"
        public static string Short(long n)
        {
            if (n < 0 && n > long.MinValue) return "-" + Short(-n);
            if (n >= 100000000L)
            {
                string eok = (n / 100000000.0).ToString("0.00", Inv);
                return TrimZero(eok) + "억";
            }
            if (n >= 10000L)
            {
                double man = Math.Round(n / 10000.0, MidpointRounding.AwayFromZero);
                return man.ToString("#,##0", Inv) + "만";
            }
            return n.ToString("#,##0", Inv);
        }

        // 12.3% 형식. b가 0이면 "-"
        public static string Pct(long a, long b)
        {
            if (b == 0) return "-";
            return (a * 100.0 / b).ToString("0.0", Inv) + "%";
        }

        private static string TrimZero(string s)
        {
            if (s.IndexOf('.') < 0) return s;
            return s.TrimEnd('0').TrimEnd('.');
        }

        private static string S(string s)
        {
            return s ?? "";
        }

        // ---------- 공통 헬퍼 ----------

        private static void Prepare(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        private static void DrawText(Graphics g, string s, Font f, Brush b, RectangleF r, StringAlignment h, StringAlignment v)
        {
            using (StringFormat sf = new StringFormat())
            {
                sf.Alignment = h;
                sf.LineAlignment = v;
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags |= StringFormatFlags.NoWrap;
                g.DrawString(S(s), f, b, r, sf);
            }
        }

        // 너비에 맞게 폰트 크기를 줄인다. 반환된 Font는 호출자가 Dispose 해야 한다.
        private static Font FitFont(Graphics g, string s, float maxSize, float minSize, bool bold, float maxWidth)
        {
            float size = maxSize;
            Font f = Theme.Font(size, bold);
            while (size > minSize)
            {
                if (g.MeasureString(S(s), f).Width <= maxWidth) break;
                f.Dispose();
                size -= 1f;
                f = Theme.Font(size, bold);
            }
            return f;
        }

        private static void DrawNoData(Graphics g, Rectangle r)
        {
            using (Font f = Theme.Font(12, false))
            using (Brush b = new SolidBrush(Theme.Muted))
            {
                DrawText(g, "데이터 없음", f, b, r, StringAlignment.Center, StringAlignment.Center);
            }
        }

        // ---------- 월별 세로 막대 ----------

        public static void DrawMonthBars(Graphics g, Rectangle r, string[] labels, long[] values)
        {
            Prepare(g);
            if (labels == null || values == null || labels.Length == 0 || values.Length == 0)
            {
                DrawNoData(g, r);
                return;
            }

            int n = Math.Min(labels.Length, values.Length);
            long max = 0;
            int maxIdx = 0;
            for (int i = 0; i < n; i++)
            {
                if (values[i] > max)
                {
                    max = values[i];
                    maxIdx = i;
                }
            }
            if (max <= 0)
            {
                DrawNoData(g, r);
                return;
            }

            const int valueH = 16;   // 막대 위 값 라벨 영역
            const int labelH = 20;   // 아래 월 라벨 영역
            Rectangle plot = Rectangle.FromLTRB(r.Left + 4, r.Top + valueH + 6, r.Right - 4, r.Bottom - labelH - 2);
            if (plot.Width < 10 || plot.Height < 10) return;

            float slot = (float)plot.Width / n;
            float barW = Math.Max(2f, slot * 0.6f);
            float valueSize = n <= 12 ? 11f : 9f;
            float labelSize = n <= 12 ? 11f : 9f;

            using (Font vf = Theme.Font(valueSize, false))
            using (Font lf = Theme.Font(labelSize, false))
            using (Brush cyan = new SolidBrush(Color.FromArgb(170, Theme.Cyan)))
            using (Brush yellow = new SolidBrush(Theme.Yellow))
            using (Brush text = new SolidBrush(Theme.Text))
            using (Brush muted = new SolidBrush(Theme.Muted))
            using (Pen line = new Pen(Theme.Line))
            {
                g.DrawLine(line, plot.Left, plot.Bottom, plot.Right, plot.Bottom);

                // 월 라벨이 서로 겹치면 여러 칸 중 한 칸만 표시한다
                int step = 1;
                float maxLabelW = 0f;
                for (int i = 0; i < n; i++)
                {
                    float w = g.MeasureString(S(labels[i]), lf).Width;
                    if (w > maxLabelW) maxLabelW = w;
                }
                if (maxLabelW > slot) step = (int)Math.Ceiling(maxLabelW / slot);

                for (int i = 0; i < n; i++)
                {
                    float cx = plot.Left + slot * i + slot / 2f;
                    float x = cx - barW / 2f;
                    float h = (float)values[i] / max * plot.Height;
                    bool isMax = (i == maxIdx);

                    if (h >= 1f)
                        g.FillRectangle(isMax ? yellow : cyan, x, plot.Bottom - h, barW, h);

                    // 값 라벨: 칸에 들어갈 때만 표시 (최대값은 항상 표시)
                    string vs = Short(values[i]);
                    float vw = g.MeasureString(vs, vf).Width;
                    if (values[i] > 0 && (vw <= slot || isMax))
                    {
                        float vwBox = Math.Max(slot, vw + 4f);
                        float vy = plot.Bottom - h - valueH - 1f;
                        if (vy < r.Top) vy = r.Top;
                        // 공유 Brush는 여기서 해제하지 않는다 (위에서 한 번만 생성)
                        DrawText(g, vs, vf, isMax ? yellow : text, new RectangleF(cx - vwBox / 2f, vy, vwBox, valueH),
                            StringAlignment.Center, StringAlignment.Center);
                    }

                    // 월 라벨
                    if (i % step == 0)
                    {
                        float lw = slot * step;
                        DrawText(g, labels[i], lf, muted,
                            new RectangleF(cx - lw / 2f, plot.Bottom + 3, lw, labelH - 2),
                            StringAlignment.Center, StringAlignment.Near);
                    }
                }
            }
        }

        // ---------- 가로 막대 (순위) ----------

        public static void DrawHBars(Graphics g, Rectangle r, IList<KeyValuePair<string, long>> data, long total)
        {
            Prepare(g);
            if (data == null || data.Count == 0)
            {
                DrawNoData(g, r);
                return;
            }
            if (r.Width < 80 || r.Height < 10) return;

            int count = data.Count;
            int rowH = Math.Min(34, r.Height / count);
            if (rowH < 6) rowH = 6;
            int blockH = rowH * count;
            int top = r.Top + Math.Max(0, (r.Height - blockH) / 2);

            int nameW = Math.Min(110, r.Width * 28 / 100);
            int valW = Math.Min(170, r.Width * 34 / 100);
            int barX0 = r.Left + nameW + 8;
            int barX1 = r.Right - valW - 8;
            int barMaxW = Math.Max(10, barX1 - barX0);
            int barH = Math.Max(4, rowH * 45 / 100);

            long max = 0;
            for (int i = 0; i < count; i++)
            {
                if (data[i].Value > max) max = data[i].Value;
            }

            using (Font nf = Theme.Font(11, false))
            using (Font vf = Theme.Font(11, false))
            using (Brush text = new SolidBrush(Theme.Text))
            using (Brush yellow = new SolidBrush(Theme.Yellow))
            using (Brush track = new SolidBrush(Color.FromArgb(40, Theme.Cyan)))
            {
                for (int i = 0; i < count; i++)
                {
                    KeyValuePair<string, long> kv = data[i];
                    bool isTop = (i == 0);
                    int y = top + i * rowH;
                    int barY = y + (rowH - barH) / 2;

                    // 왼쪽 이름
                    DrawText(g, kv.Key, nf, text, new RectangleF(r.Left, y, nameW, rowH),
                        StringAlignment.Near, StringAlignment.Center);

                    // 가로 막대 트랙 + 막대
                    g.FillRectangle(track, barX0, barY, barMaxW, barH);
                    int w = 0;
                    if (max > 0 && kv.Value > 0)
                    {
                        w = (int)Math.Round((double)kv.Value / max * barMaxW);
                        if (w < 2) w = 2;
                        if (w > barMaxW) w = barMaxW;
                    }
                    if (w >= 1)
                    {
                        Color c1 = isTop ? Color.FromArgb(120, Theme.Yellow) : Color.FromArgb(90, Theme.Cyan);
                        Color c2 = isTop ? Theme.Yellow : Theme.Cyan;
                        using (LinearGradientBrush lb = new LinearGradientBrush(
                            new Rectangle(barX0, barY, w, barH), c1, c2, 0f, false))
                        {
                            g.FillRectangle(lb, barX0, barY, w, barH);
                        }
                    }

                    // 오른쪽 "금액 · 비중"
                    string vt = Short(kv.Value) + " · " + Pct(kv.Value, total);
                    DrawText(g, vt, vf, isTop ? yellow : text, new RectangleF(r.Right - valW, y, valW, rowH),
                        StringAlignment.Far, StringAlignment.Center);
                }
            }
        }

        // ---------- 도넛 + 범례 ----------

        public static void DrawDonut(Graphics g, Rectangle r, IList<KeyValuePair<string, long>> data, long total)
        {
            Prepare(g);
            if (data == null || data.Count == 0)
            {
                DrawNoData(g, r);
                return;
            }
            if (r.Width < 120 || r.Height < 60) return;

            int d = Math.Min(r.Height - 12, (int)(r.Width * 0.42));
            if (d < 40) d = 40;
            Rectangle ring = new Rectangle(r.Left + 8, r.Top + (r.Height - d) / 2, d, d);
            float thick = Math.Max(10f, d * 0.22f);
            float mid = d / 2f - thick / 2f;             // 선(stroke) 중심 반지름
            float cxf = ring.Left + d / 2f;
            float cyf = ring.Top + d / 2f;
            RectangleF arcRect = new RectangleF(cxf - mid, cyf - mid, mid * 2f, mid * 2f);
            float hole = d - thick * 2f;

            double sumPos = 0;
            for (int i = 0; i < data.Count; i++)
            {
                if (data[i].Value > 0) sumPos += data[i].Value;
            }

            using (Pen trackPen = new Pen(Color.FromArgb(40, Theme.Cyan), thick))
            {
                g.DrawEllipse(trackPen, arcRect);
            }

            if (sumPos > 0)
            {
                float start = -90f;
                for (int i = 0; i < data.Count; i++)
                {
                    long v = data[i].Value;
                    if (v <= 0) continue;
                    float sweep = (float)(360.0 * v / sumPos);
                    using (Pen p = new Pen(Theme.Palette[i % Theme.Palette.Length], thick))
                    {
                        if (sweep >= 359.9f) g.DrawEllipse(p, arcRect);
                        else g.DrawArc(p, arcRect, start, sweep);
                    }
                    start += sweep;
                }
            }

            // 가운데: 총매출
            string center = Short(total);
            using (Font cf = FitFont(g, center, 20f, 9f, true, hole * 0.78f))
            using (Font lf = Theme.Font(10, false))
            using (Brush tb = new SolidBrush(Theme.Text))
            using (Brush mb = new SolidBrush(Theme.Muted))
            {
                DrawText(g, center, cf, tb, new RectangleF(cxf - hole / 2f, cyf - 24f, hole, 26f),
                    StringAlignment.Center, StringAlignment.Center);
                DrawText(g, "총매출", lf, mb, new RectangleF(cxf - hole / 2f, cyf + 4f, hole, 16f),
                    StringAlignment.Center, StringAlignment.Near);
            }

            // 오른쪽 범례
            Rectangle legend = Rectangle.FromLTRB(ring.Right + 20, r.Top + 4, r.Right - 4, r.Bottom - 4);
            if (legend.Width < 60 || legend.Height < 12) return;

            int rowH = Math.Max(12, Math.Min(28, legend.Height / data.Count));
            int valW = Math.Min(170, legend.Width / 2);
            int nameW = Math.Max(20, legend.Width - valW - 18);
            int legendTop = legend.Top + Math.Max(0, (legend.Height - rowH * data.Count) / 2);

            using (Font nf = Theme.Font(11, false))
            using (Font vf = Theme.Font(11, false))
            using (Brush text = new SolidBrush(Theme.Text))
            using (Brush muted = new SolidBrush(Theme.Muted))
            {
                for (int i = 0; i < data.Count; i++)
                {
                    int y = legendTop + i * rowH;
                    if (y + rowH > legend.Bottom + 1) break;
                    KeyValuePair<string, long> kv = data[i];

                    using (Brush dot = new SolidBrush(Theme.Palette[i % Theme.Palette.Length]))
                    {
                        int dotD = Math.Min(10, rowH - 2);
                        g.FillEllipse(dot, legend.Left, y + (rowH - dotD) / 2f, dotD, dotD);
                    }

                    DrawText(g, kv.Key, nf, text, new RectangleF(legend.Left + 16, y, nameW, rowH),
                        StringAlignment.Near, StringAlignment.Center);

                    string vt = Short(kv.Value) + " · " + Pct(kv.Value, total);
                    DrawText(g, vt, vf, muted, new RectangleF(legend.Right - valW, y, valW, rowH),
                        StringAlignment.Far, StringAlignment.Center);
                }
            }
        }

        // ---------- KPI 타일 ----------

        public static void DrawKpiTile(Graphics g, Rectangle r, string label, string value, Color valueColor)
        {
            Prepare(g);
            if (r.Width < 20 || r.Height < 20) return;

            const int pad = 12;
            float innerW = r.Width - pad * 2;

            using (Brush panel = new SolidBrush(Theme.Panel))
            using (Pen border = new Pen(Theme.Cyan, 1f))
            using (Font lf = Theme.Font(12, false))
            using (Brush mb = new SolidBrush(Theme.Muted))
            using (Brush vb = new SolidBrush(valueColor))
            {
                g.FillRectangle(panel, r);
                g.DrawRectangle(border, r.Left + 0.5f, r.Top + 0.5f, r.Width - 1f, r.Height - 1f);

                DrawText(g, label, lf, mb, new RectangleF(r.Left + pad, r.Top + pad, innerW, 18f),
                    StringAlignment.Near, StringAlignment.Near);

                float valTop = r.Top + pad + 22f;
                float valH = r.Bottom - pad - valTop;
                float maxSize = Math.Min(40f, valH);
                if (maxSize < 10f) maxSize = 10f;

                using (Font vf = FitFont(g, value, maxSize, 10f, true, innerW))
                {
                    DrawText(g, value, vf, vb, new RectangleF(r.Left + pad, valTop, innerW, valH),
                        StringAlignment.Near, StringAlignment.Center);
                }
            }
        }
    }
}
