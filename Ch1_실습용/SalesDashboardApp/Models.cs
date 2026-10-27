using System;
using System.Collections.Generic;
using System.Drawing;

namespace SalesDash
{
    // 주문 1건 (엑셀 1행)
    public class Order
    {
        public string Id;
        public DateTime Date;
        public string Region, Rep, Channel, Product;
        public long Price, Qty, Amount;
    }

    // 엑셀 불러오기 결과. Errors가 있으면 데이터를 쓰지 않는다.
    public class LoadResult
    {
        public List<Order> Items = new List<Order>();
        public List<string> Errors = new List<string>();
        public List<string> Warns = new List<string>();
        public string SheetName;
    }

    // 다크 관제 화면 색상 (HTML 대시보드와 동일 팔레트)
    public static class Theme
    {
        public static readonly Color Bg = ColorTranslator.FromHtml("#070d1f");
        public static readonly Color Panel = ColorTranslator.FromHtml("#0b1630");
        public static readonly Color Cyan = ColorTranslator.FromHtml("#19e3ff");
        public static readonly Color Yellow = ColorTranslator.FromHtml("#ffd426");
        public static readonly Color Text = ColorTranslator.FromHtml("#e6f4ff");
        public static readonly Color Muted = ColorTranslator.FromHtml("#7f95b5");
        public static readonly Color Line = Color.FromArgb(90, 25, 227, 255);
        public static readonly Color Pink = ColorTranslator.FromHtml("#ff5d8f");

        public static readonly Color[] Palette = new Color[]
        {
            ColorTranslator.FromHtml("#19e3ff"), ColorTranslator.FromHtml("#ffd426"),
            ColorTranslator.FromHtml("#ff5d8f"), ColorTranslator.FromHtml("#7cf29a"),
            ColorTranslator.FromHtml("#b48cff"), ColorTranslator.FromHtml("#ff9f43"),
            ColorTranslator.FromHtml("#4d9bff"), ColorTranslator.FromHtml("#e0e6ff")
        };

        public static Font Font(float size, bool bold)
        {
            return new Font("Malgun Gothic", size, bold ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }
    }
}
