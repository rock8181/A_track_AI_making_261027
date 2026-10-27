using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace SalesDash
{
    // 엑셀(.xlsx) 읽기 모듈. 엑셀이 열어 둔 파일도 읽을 수 있도록 공유 읽기로 연다.
    // 외부 라이브러리 없이 zip(System.IO.Compression)과 정규식으로 XML을 읽는다.
    // 규칙은 참고 구현 sd/xlsx.js (parseXlsx + normalizeRows)와 같다.
    public static class XlsxReader
    {
        static readonly string[] Required = new string[] { "주문번호", "주문일자", "지역", "담당자", "채널", "제품", "단가", "수량", "매출액" };

        // 파일을 읽어 LoadResult를 돌려준다. 예외는 던지지 않고 Errors에 담는다.
        public static LoadResult Load(string path, DateTime today)
        {
            var result = new LoadResult();
            try
            {
                string sheetName;
                Dictionary<int, List<object>> rows;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    rows = ParseSheet(zip, out sheetName);
                }
                result.SheetName = sheetName;
                Normalize(rows, today, result);
            }
            catch (FileNotFoundException)
            {
                result.Errors.Add("파일을 찾을 수 없습니다: " + path);
            }
            catch (DirectoryNotFoundException)
            {
                result.Errors.Add("파일 경로를 찾을 수 없습니다: " + path);
            }
            catch (InvalidDataException)
            {
                result.Errors.Add("xlsx(zip) 형식이 아니거나 파일이 손상되었습니다.");
            }
            catch (IOException ex)
            {
                result.Errors.Add("파일을 읽는 중 오류가 발생했습니다: " + ex.Message);
            }
            catch (Exception ex)
            {
                result.Errors.Add("엑셀 파일을 읽지 못했습니다: " + ex.Message);
            }
            return result;
        }

        // ---- zip / XML 파싱 ----

        static string ReadEntry(ZipArchive zip, string name)
        {
            ZipArchiveEntry entry = zip.GetEntry(name);
            if (entry == null) return null;
            using (var s = entry.Open())
            using (var ms = new MemoryStream())
            {
                s.CopyTo(ms);
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        // 시트 셀 값을 행 번호(1부터) -> 열 배열로 돌려준다. 값 타입: string, double, bool, null
        static Dictionary<int, List<object>> ParseSheet(ZipArchive zip, out string sheetName)
        {
            string wb = ReadEntry(zip, "xl/workbook.xml");
            if (wb == null) throw new InvalidDataException("엑셀 통합 문서가 아닙니다");
            string rels = ReadEntry(zip, "xl/_rels/workbook.xml.rels") ?? "";

            var sheets = new List<KeyValuePair<string, string>>(); // name, rid
            foreach (Match m in Regex.Matches(wb, @"<sheet\s([^>]*?)/?>"))
            {
                string attrs = m.Groups[1].Value;
                sheets.Add(new KeyValuePair<string, string>(XmlDecode(Attr(attrs, "name") ?? ""), Attr(attrs, "r:id")));
            }
            if (sheets.Count == 0) throw new InvalidDataException("시트가 없습니다");

            KeyValuePair<string, string> sh = sheets[0];
            foreach (var s in sheets)
            {
                if (s.Key == "Sales") { sh = s; break; }
            }
            sheetName = sh.Key;

            string target = null;
            foreach (Match m in Regex.Matches(rels, @"<Relationship\s([^>]*?)/?>"))
            {
                if (Attr(m.Groups[1].Value, "Id") == sh.Value)
                {
                    target = Attr(m.Groups[1].Value, "Target");
                    break;
                }
            }
            string path;
            if (!string.IsNullOrEmpty(target))
                path = target.StartsWith("/") ? target.Substring(1) : "xl/" + target;
            else
                path = "xl/worksheets/sheet1.xml";

            string xml = ReadEntry(zip, path);
            if (xml == null) throw new InvalidDataException("시트 데이터를 찾을 수 없습니다");

            string sst = ReadEntry(zip, "xl/sharedStrings.xml");
            var strs = new List<string>();
            if (sst != null)
            {
                foreach (Match m in Regex.Matches(sst, @"<si(?:\s[^>]*)?>([\s\S]*?)</si>"))
                    strs.Add(TextOf(m.Groups[1].Value));
            }

            var rows = new Dictionary<int, List<object>>();
            foreach (Match r in Regex.Matches(xml, @"<row\s([^>]*?)(?:/>|>([\s\S]*?)</row>)"))
            {
                string rAttr = Attr(r.Groups[1].Value, "r");
                int rn;
                if (!int.TryParse(rAttr, NumberStyles.Integer, CultureInfo.InvariantCulture, out rn))
                    rn = rows.Count + 1;

                var row = new List<object>();
                string body = r.Groups[2].Value;
                foreach (Match c in Regex.Matches(body, @"<c\s([^>]*?)(?:/>|>([\s\S]*?)</c>)"))
                {
                    string cAttrs = c.Groups[1].Value;
                    string cRef = Attr(cAttrs, "r");
                    if (string.IsNullOrEmpty(cRef)) continue;
                    string t = Attr(cAttrs, "t");
                    string cBody = c.Groups[2].Value;
                    Match vm = Regex.Match(cBody, @"<v>([\s\S]*?)</v>");
                    string v = vm.Success ? vm.Groups[1].Value : null;

                    object val = null;
                    if (t == "s")
                    {
                        int si;
                        if (v != null && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out si) && si >= 0 && si < strs.Count)
                            val = strs[si];
                    }
                    else if (t == "inlineStr")
                    {
                        val = TextOf(cBody);
                    }
                    else if (t == "str" || t == "e")
                    {
                        val = v != null ? XmlDecode(v) : null;
                    }
                    else if (t == "b")
                    {
                        val = v == "1";
                    }
                    else if (v != null && v != "")
                    {
                        double d;
                        if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                            val = d;
                        else
                            val = v;
                    }

                    int ci = ColIndex(cRef);
                    while (row.Count <= ci) row.Add(null);
                    row[ci] = val;
                }
                rows[rn] = row;
            }
            return rows;
        }

        // 태그 속성 문자열에서 name="값"을 찾는다. 없으면 null.
        static string Attr(string attrs, string name)
        {
            Match m = Regex.Match(attrs, "(?:^|\\s)" + Regex.Escape(name) + "=\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        // 공유 문자열 / inlineStr 본문에서 <t> 텍스트를 이어 붙인다. 발음표기(rPh)는 제외.
        static string TextOf(string x)
        {
            string noPh = Regex.Replace(x, @"<rPh[\s\S]*?</rPh>", "");
            var sb = new StringBuilder();
            foreach (Match m in Regex.Matches(noPh, @"<t(?:\s[^>]*)?>([\s\S]*?)</t>"))
                sb.Append(XmlDecode(m.Groups[1].Value));
            return sb.ToString();
        }

        static string XmlDecode(string s)
        {
            s = Regex.Replace(s, @"&#x([0-9a-fA-F]+);", HexEntity);
            s = Regex.Replace(s, @"&#(\d+);", DecEntity);
            return s.Replace("&lt;", "<").Replace("&gt;", ">").Replace("&quot;", "\"").Replace("&apos;", "'").Replace("&amp;", "&");
        }

        static string HexEntity(Match m)
        {
            return CodePointToString(m.Value, m.Groups[1].Value, 16);
        }

        static string DecEntity(Match m)
        {
            return CodePointToString(m.Value, m.Groups[1].Value, 10);
        }

        static string CodePointToString(string original, string digits, int radix)
        {
            try
            {
                int cp = Convert.ToInt32(digits, radix);
                return char.ConvertFromUtf32(cp);
            }
            catch (Exception)
            {
                return original; // 잘못된 코드 포인트는 그대로 둔다
            }
        }

        // "AB12" -> 27 (0부터)
        static int ColIndex(string cellRef)
        {
            int n = 0;
            foreach (char ch in cellRef)
            {
                if (ch >= 'A' && ch <= 'Z') n = n * 26 + (ch - 'A' + 1);
                else if (ch >= 'a' && ch <= 'z') n = n * 26 + (ch - 'a' + 1);
                else break;
            }
            return n - 1;
        }

        // ---- 행 정규화 (normalizeRows 이식) ----

        static void Normalize(Dictionary<int, List<object>> rows, DateTime today, LoadResult result)
        {
            if (rows.Count == 0)
            {
                result.Errors.Add("데이터가 비어 있습니다");
                return;
            }

            var head = new List<string>();
            List<object> headRow;
            if (rows.TryGetValue(1, out headRow))
            {
                foreach (object h in headRow) head.Add(Str(h).Trim());
            }

            var miss = new List<string>();
            var ix = new Dictionary<string, int>();
            foreach (string k in Required)
            {
                int idx = head.IndexOf(k);
                if (idx < 0) miss.Add(k);
                ix[k] = idx;
            }
            if (miss.Count > 0)
            {
                result.Errors.Add("필수 컬럼이 없습니다: " + string.Join(", ", miss) + " (첫 행에 " + string.Join("/", Required) + " 필요)");
                return;
            }

            DateTime limit = today.Date;
            string limitStr = limit.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var skipped = new List<int>();
            var mism = new List<string>();
            var dup = new List<string>();
            var future = new List<string>();
            var seen = new HashSet<string>();

            var keys = new List<int>(rows.Keys);
            keys.Sort();
            foreach (int line in keys)
            {
                if (line == 1) continue; // 헤더
                List<object> r = rows[line];

                if (IsBlankRow(r, ix)) continue; // 빈 행은 조용히 무시

                string id = Str(Get(r, ix["주문번호"])).Trim();
                DateTime date;
                bool dateOk = ToDate(Get(r, ix["주문일자"]), out date);
                double price = ToNum(Get(r, ix["단가"]));
                double qty = ToNum(Get(r, ix["수량"]));
                double amount = ToNum(Get(r, ix["매출액"]));

                if (id == "" || !dateOk || double.IsNaN(price) || double.IsNaN(qty)
                    || Txt(r, ix, "지역") == "" || Txt(r, ix, "채널") == ""
                    || Txt(r, ix, "제품") == "" || Txt(r, ix, "담당자") == "")
                {
                    skipped.Add(line);
                    continue;
                }

                if (double.IsNaN(amount))
                {
                    amount = price * qty;
                }
                else if (JsRound(price * qty) != JsRound(amount))
                {
                    mism.Add(id);
                }

                if (seen.Contains(id)) dup.Add(id);
                seen.Add(id);

                if (date > limit) future.Add(id);

                var order = new Order();
                order.Id = id;
                order.Date = date;
                order.Region = Txt(r, ix, "지역");
                order.Rep = Txt(r, ix, "담당자");
                order.Channel = Txt(r, ix, "채널");
                order.Product = Txt(r, ix, "제품");
                order.Price = (long)JsRound(price);
                order.Qty = (long)JsRound(qty);
                order.Amount = (long)JsRound(amount);
                result.Items.Add(order);
            }

            if (result.Items.Count == 0)
            {
                string msg = "유효한 주문 행이 없습니다";
                if (skipped.Count > 0) msg += " (제외된 행 " + skipped.Count + "개)";
                result.Errors.Add(msg);
            }
            if (skipped.Count > 0)
            {
                var nums = new List<string>();
                foreach (int n in skipped) nums.Add(n.ToString(CultureInfo.InvariantCulture));
                result.Warns.Add("값이 비었거나 형식이 틀려 제외한 행 " + skipped.Count + "개 (엑셀 행 번호: " + Ex(nums) + ")");
            }
            if (mism.Count > 0)
                result.Warns.Add("단가×수량과 매출액이 다른 주문 " + mism.Count + "건 (매출액 열 값을 그대로 사용): " + Ex(mism));
            if (dup.Count > 0)
                result.Warns.Add("주문번호가 중복된 주문 " + dup.Count + "건 (모두 포함됨): " + Ex(dup));
            if (future.Count > 0)
                result.Warns.Add("오늘(" + limitStr + ") 이후 날짜의 주문 " + future.Count + "건 — 입력 오류 가능성: " + Ex(future));
        }

        static bool IsBlankRow(List<object> r, Dictionary<string, int> ix)
        {
            foreach (string k in Required)
            {
                object v = Get(r, ix[k]);
                if (v != null && Str(v).Trim() != "") return false;
            }
            return true;
        }

        static object Get(List<object> r, int idx)
        {
            return idx >= 0 && idx < r.Count ? r[idx] : null;
        }

        static string Txt(List<object> r, Dictionary<string, int> ix, string key)
        {
            return Str(Get(r, ix[key])).Trim();
        }

        // 셀 값을 문자열로. null은 "".
        static string Str(object v)
        {
            if (v == null) return "";
            if (v is double) return ((double)v).ToString("R", CultureInfo.InvariantCulture);
            if (v is bool) return (bool)v ? "TRUE" : "FALSE";
            return v.ToString();
        }

        // 엑셀 일련번호(20000~80000, 1899-12-30 기준) 또는 YYYY-MM-DD / YYYY/MM/DD / YYYY.MM.DD 텍스트
        static bool ToDate(object v, out DateTime date)
        {
            date = DateTime.MinValue;
            if (v is double)
            {
                double n = (double)v;
                if (n > 20000 && n < 80000)
                {
                    date = new DateTime(1899, 12, 30).AddDays(Math.Floor(n));
                    return true;
                }
                return false;
            }
            Match m = Regex.Match(Str(v).Trim(), @"^(\d{4})[-./](\d{1,2})[-./](\d{1,2})");
            if (!m.Success) return false;
            try
            {
                date = new DateTime(int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
                                    int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                                    int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture));
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        // 숫자 변환. 숫자가 아니면 NaN. "1,234", "원", 공백은 제거 후 변환.
        static double ToNum(object v)
        {
            if (v is double) return (double)v;
            string s = Regex.Replace(Str(v), @"[,\s원]", "");
            double d;
            if (s != "" && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out d)
                && !double.IsNaN(d) && !double.IsInfinity(d))
                return d;
            return double.NaN;
        }

        // JS Math.round와 같은 방식 (.5는 위로)
        static double JsRound(double x)
        {
            return Math.Floor(x + 0.5);
        }

        static string Ex(List<string> a)
        {
            var sb = new StringBuilder();
            int shown = Math.Min(5, a.Count);
            for (int i = 0; i < shown; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(a[i]);
            }
            if (a.Count > 5) sb.Append(" 외 " + (a.Count - 5) + "건");
            return sb.ToString();
        }
    }
}
