using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SalesDash
{
    // 스크롤 영역 안에 놓이는 그리기 전용 컨트롤
    public class Canvas : Control
    {
        public List<Block> Blocks = new List<Block>();

        public Canvas()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Bg);
            DashboardRenderer.DrawAll(e.Graphics, Blocks, e.ClipRectangle);
        }
    }

    public class MainForm : Form
    {
        const string All = "전체";

        readonly DashboardRenderer rend = new DashboardRenderer();
        readonly Canvas canvas = new Canvas();
        readonly Panel scroll = new Panel();
        readonly ComboBox cbRegion = new ComboBox(), cbProduct = new ComboBox(), cbChannel = new ComboBox();
        readonly Label status = new Label();
        List<Order> all = new List<Order>();
        List<string> warns = new List<string>();
        string fileName = "";
        bool updating;

        public MainForm()
        {
            Text = "매출 대시보드";
            Width = 1280; Height = 860; MinimumSize = new Size(980, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg; ForeColor = Theme.Text;
            Font = new Font("Malgun Gothic", 9f);
            AllowDrop = true;
            DragEnter += (s, e) => { if (e.Data.GetDataPresent(DataFormats.FileDrop)) e.Effect = DragDropEffects.Copy; };
            DragDrop += (s, e) => {
                var f = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (f != null && f.Length > 0) LoadFile(f[0]);
            };

            var bar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 48, BackColor = Theme.Panel, Padding = new Padding(10, 9, 10, 0), WrapContents = false };
            bar.Controls.Add(MakeButton("엑셀 불러오기", Theme.Cyan, OpenDialog));
            bar.Controls.Add(MakeButton("PDF 저장", Theme.Yellow, SavePdf));
            bar.Controls.Add(MakeLabel("지역")); bar.Controls.Add(MakeCombo(cbRegion));
            bar.Controls.Add(MakeLabel("제품")); bar.Controls.Add(MakeCombo(cbProduct));
            bar.Controls.Add(MakeLabel("채널")); bar.Controls.Add(MakeCombo(cbChannel));
            bar.Controls.Add(MakeButton("초기화", Theme.Muted, ResetFilters));

            status.Dock = DockStyle.Top; status.Height = 24; status.Padding = new Padding(12, 3, 0, 0);
            status.BackColor = Theme.Bg; status.ForeColor = Theme.Muted; status.Cursor = Cursors.Default;
            status.Text = "엑셀 파일을 불러오세요 (.xlsx)";
            status.Click += (s, e) => ShowWarns();

            scroll.Dock = DockStyle.Fill; scroll.AutoScroll = true; scroll.BackColor = Theme.Bg;
            scroll.Controls.Add(canvas);
            scroll.Resize += (s, e) => Relayout();

            Controls.Add(scroll);
            Controls.Add(status);
            Controls.Add(bar);

            Load += (s, e) => { AutoLoadSample(); Relayout(); };
        }

        Button MakeButton(string text, Color c, Action act)
        {
            var b = new Button { Text = text, AutoSize = true, FlatStyle = FlatStyle.Flat, ForeColor = c, BackColor = Theme.Bg, Height = 30, Padding = new Padding(8, 0, 8, 0), Cursor = Cursors.Hand };
            b.FlatAppearance.BorderColor = c;
            b.Click += (s, e) => act();
            return b;
        }

        Label MakeLabel(string t) { return new Label { Text = t, AutoSize = true, ForeColor = Theme.Muted, Margin = new Padding(14, 7, 2, 0) }; }

        ComboBox MakeCombo(ComboBox c)
        {
            c.DropDownStyle = ComboBoxStyle.DropDownList; c.Width = 110; c.Margin = new Padding(2, 4, 0, 0);
            c.Items.Add(All); c.SelectedIndex = 0;
            c.SelectedIndexChanged += (s, e) => { if (!updating) Refresh2(); };
            return c;
        }

        // exe 옆 또는 상위 폴더의 sales.xlsx가 있으면 시작할 때 자동으로 불러온다
        void AutoLoadSample()
        {
            string b = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var d in new[] { b, Path.GetFullPath(Path.Combine(b, "..")) })
            {
                string p = Path.Combine(d, "sales.xlsx");
                if (File.Exists(p)) { LoadFile(p); return; }
            }
        }

        void OpenDialog()
        {
            using (var dlg = new OpenFileDialog { Title = "매출 엑셀 선택", Filter = "엑셀 파일 (*.xlsx)|*.xlsx" })
                if (dlg.ShowDialog(this) == DialogResult.OK) LoadFile(dlg.FileName);
        }

        void LoadFile(string path)
        {
            LoadResult r;
            Cursor = Cursors.WaitCursor;
            try { r = XlsxReader.Load(path, DateTime.Today); }
            finally { Cursor = Cursors.Default; }

            if (r.Errors.Count > 0)
            {
                MessageBox.Show(this, string.Join("\n", r.Errors.ToArray()), "엑셀을 불러올 수 없습니다", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return; // 기존 화면 유지
            }
            all = r.Items; warns = r.Warns; fileName = Path.GetFileName(path);
            rend.Source = fileName;
            rend.Months = MonthRange(all);
            FillCombo(cbRegion, all.Select(o => o.Region));
            FillCombo(cbProduct, all.Select(o => o.Product));
            FillCombo(cbChannel, all.Select(o => o.Channel));
            Refresh2();
        }

        // 최소~최대 월 사이를 빠짐없이 채운 YYYY-MM 목록
        static List<string> MonthRange(List<Order> items)
        {
            var res = new List<string>();
            if (items.Count == 0) return res;
            var lo = items.Min(o => o.Date); var hi = items.Max(o => o.Date);
            for (var d = new DateTime(lo.Year, lo.Month, 1); d <= hi; d = d.AddMonths(1))
                res.Add(d.ToString("yyyy-MM", CultureInfo.InvariantCulture));
            return res;
        }

        void FillCombo(ComboBox c, IEnumerable<string> vals)
        {
            updating = true;
            c.Items.Clear(); c.Items.Add(All);
            foreach (var v in vals.Distinct().OrderBy(x => x, StringComparer.Ordinal)) c.Items.Add(v);
            c.SelectedIndex = 0;
            updating = false;
        }

        void ResetFilters()
        {
            updating = true;
            cbRegion.SelectedIndex = 0; cbProduct.SelectedIndex = 0; cbChannel.SelectedIndex = 0;
            updating = false;
            Refresh2();
        }

        static bool Pick(ComboBox c, string v) { return c.SelectedIndex <= 0 || (string)c.SelectedItem == v; }

        void Refresh2()
        {
            rend.HasData = all.Count > 0;
            rend.Items = all.Where(o => Pick(cbRegion, o.Region) && Pick(cbProduct, o.Product) && Pick(cbChannel, o.Channel)).ToList();
            if (all.Count == 0) { status.Text = "엑셀 파일을 불러오세요 (.xlsx)"; status.ForeColor = Theme.Muted; }
            else if (warns.Count > 0) { status.Text = "⚠ " + fileName + " — 확인이 필요한 알림 " + warns.Count + "건 (여기를 클릭하면 내용을 봅니다)"; status.ForeColor = Theme.Yellow; status.Cursor = Cursors.Hand; }
            else { status.Text = fileName + " — 주문 " + all.Count + "건을 불러왔습니다"; status.ForeColor = Theme.Muted; status.Cursor = Cursors.Default; }
            Relayout();
        }

        void ShowWarns()
        {
            if (warns.Count > 0)
                MessageBox.Show(this, string.Join("\n\n", warns.ToArray()), "불러오기 알림", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        void Relayout()
        {
            int w = Math.Max(900, scroll.ClientSize.Width);
            canvas.Blocks = rend.Build(w);
            canvas.SetBounds(0, 0, w, DashboardRenderer.TotalHeight(canvas.Blocks) + 8);
            canvas.Invalidate();
        }

        void SavePdf()
        {
            if (all.Count == 0) { MessageBox.Show(this, "먼저 엑셀 파일을 불러오세요.", "PDF 저장", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            string path;
            using (var dlg = new SaveFileDialog { Title = "PDF 저장", Filter = "PDF 파일 (*.pdf)|*.pdf", FileName = "매출대시보드_" + DateTime.Today.ToString("yyyyMMdd") + ".pdf" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                path = dlg.FileName;
            }
            Cursor = Cursors.WaitCursor;
            List<Bitmap> pages = null;
            try
            {
                pages = rend.RenderPages(1100, 2f);
                PdfWriter.SavePages(path, pages);
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show(this, "PDF를 저장하지 못했습니다.\n" + ex.Message + "\n(같은 이름의 PDF가 열려 있으면 닫고 다시 시도하세요)", "PDF 저장", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            finally
            {
                if (pages != null) foreach (var p in pages) p.Dispose();
                Cursor = Cursors.Default;
            }
            if (MessageBox.Show(this, "저장했습니다 (" + pages.Count + "쪽).\n지금 열어 볼까요?\n\n" + path, "PDF 저장", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
