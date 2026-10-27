using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace SalesDash
{
    // 비트맵 페이지를 JPEG 이미지로 넣은 PDF를 직접 작성한다 (외부 라이브러리 없음).
    // 페이지 1장 = 이미지 1장, 페이지 크기는 가로 A4(842 x 595 pt).
    public static class PdfWriter
    {
        private const int PageW = 842;
        private const int PageH = 595;

        // 객체 번호: 1 = Catalog, 2 = Pages,
        // i번째(0부터) 페이지: Page = 3+3i, Image = 4+3i, Content = 5+3i
        public static void SavePages(string path, IList<Bitmap> pages)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (pages == null || pages.Count == 0)
                throw new ArgumentException("PDF로 저장할 페이지가 없습니다.", "pages");
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i] == null)
                    throw new ArgumentException("null 페이지가 포함되어 있습니다. 인덱스: " + i.ToString(CultureInfo.InvariantCulture), "pages");
            }

            int n = pages.Count;
            int objCount = 2 + 3 * n;

            // 각 페이지를 24bpp RGB로 맞춘 뒤 JPEG(품질 90)로 인코딩한다.
            byte[][] jpegs = new byte[n][];
            int[] widths = new int[n];
            int[] heights = new int[n];
            for (int i = 0; i < n; i++)
            {
                using (Bitmap rgb = ToRgb24(pages[i]))
                {
                    jpegs[i] = EncodeJpeg(rgb);
                    widths[i] = rgb.Width;
                    heights[i] = rgb.Height;
                }
            }

            long[] offsets = new long[objCount + 1];

            using (MemoryStream ms = new MemoryStream())
            {
                // 헤더 + 바이너리 주석
                WriteAscii(ms, "%PDF-1.4\n");
                ms.Write(new byte[] { (byte)'%', 0xE2, 0xE3, 0xCF, 0xD3, (byte)'\n' }, 0, 6);

                // 1: Catalog
                StartObj(ms, offsets, 1);
                WriteAscii(ms, "<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");

                // 2: Pages
                StringBuilder kids = new StringBuilder();
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) kids.Append(' ');
                    kids.Append((3 + 3 * i).ToString(CultureInfo.InvariantCulture)).Append(" 0 R");
                }
                StartObj(ms, offsets, 2);
                WriteAscii(ms, "<< /Type /Pages /Kids [" + kids.ToString() + "] /Count "
                    + n.ToString(CultureInfo.InvariantCulture) + " >>\nendobj\n");

                for (int i = 0; i < n; i++)
                {
                    int pageObj = 3 + 3 * i;
                    int imgObj = 4 + 3 * i;
                    int contObj = 5 + 3 * i;

                    // 비율 유지, 페이지 안에 최대 크기로 가운데 배치
                    double w = widths[i];
                    double h = heights[i];
                    double scale = Math.Min((double)PageW / w, (double)PageH / h);
                    double dw = w * scale;
                    double dh = h * scale;
                    double x = (PageW - dw) / 2.0;
                    double y = (PageH - dh) / 2.0;

                    string content = "q " + F(dw) + " 0 0 " + F(dh) + " " + F(x) + " " + F(y) + " cm /Im0 Do Q\n";
                    int contentLen = Encoding.ASCII.GetByteCount(content);

                    // Page
                    StartObj(ms, offsets, pageObj);
                    WriteAscii(ms, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + PageW.ToString(CultureInfo.InvariantCulture)
                        + " " + PageH.ToString(CultureInfo.InvariantCulture) + "] /Resources << /XObject << /Im0 "
                        + imgObj.ToString(CultureInfo.InvariantCulture) + " 0 R >> >> /Contents "
                        + contObj.ToString(CultureInfo.InvariantCulture) + " 0 R >>\nendobj\n");

                    // Image (JPEG 바이트를 그대로 넣는다)
                    byte[] jpeg = jpegs[i];
                    StartObj(ms, offsets, imgObj);
                    WriteAscii(ms, "<< /Type /XObject /Subtype /Image /Width " + widths[i].ToString(CultureInfo.InvariantCulture)
                        + " /Height " + heights[i].ToString(CultureInfo.InvariantCulture)
                        + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length "
                        + jpeg.Length.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
                    ms.Write(jpeg, 0, jpeg.Length);
                    WriteAscii(ms, "\nendstream\nendobj\n");

                    // Content stream
                    StartObj(ms, offsets, contObj);
                    WriteAscii(ms, "<< /Length " + contentLen.ToString(CultureInfo.InvariantCulture) + " >>\nstream\n");
                    WriteAscii(ms, content);
                    WriteAscii(ms, "endstream\nendobj\n");
                }

                // xref (각 항목은 정확히 20바이트)
                long xrefPos = ms.Position;
                WriteAscii(ms, "xref\n0 " + (objCount + 1).ToString(CultureInfo.InvariantCulture) + "\n");
                WriteAscii(ms, "0000000000 65535 f \n");
                for (int obj = 1; obj <= objCount; obj++)
                {
                    WriteAscii(ms, offsets[obj].ToString("D10", CultureInfo.InvariantCulture) + " 00000 n \n");
                }

                // trailer
                WriteAscii(ms, "trailer\n<< /Size " + (objCount + 1).ToString(CultureInfo.InvariantCulture)
                    + " /Root 1 0 R >>\nstartxref\n" + xrefPos.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");

                File.WriteAllBytes(path, ms.ToArray());
            }
        }

        private static string F(double v)
        {
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static void StartObj(MemoryStream ms, long[] offsets, int objNo)
        {
            offsets[objNo] = ms.Position;
            WriteAscii(ms, objNo.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
        }

        private static void WriteAscii(MemoryStream ms, string s)
        {
            byte[] b = Encoding.ASCII.GetBytes(s);
            ms.Write(b, 0, b.Length);
        }

        private static Bitmap ToRgb24(Bitmap src)
        {
            Bitmap dst = new Bitmap(src.Width, src.Height, PixelFormat.Format24bppRgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.Clear(Color.White);
                g.DrawImage(src, 0, 0, src.Width, src.Height);
            }
            return dst;
        }

        private static byte[] EncodeJpeg(Bitmap bmp)
        {
            ImageCodecInfo codec = null;
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
            for (int i = 0; i < codecs.Length; i++)
            {
                if (codecs[i].MimeType == "image/jpeg")
                {
                    codec = codecs[i];
                    break;
                }
            }
            if (codec == null)
                throw new InvalidOperationException("JPEG 인코더를 찾을 수 없습니다.");

            using (EncoderParameters ep = new EncoderParameters(1))
            {
                ep.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                using (MemoryStream ms = new MemoryStream())
                {
                    bmp.Save(ms, codec, ep);
                    return ms.ToArray();
                }
            }
        }
    }
}
