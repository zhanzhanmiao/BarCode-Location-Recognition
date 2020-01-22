using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Runtime.InteropServices;
//using ZBar;

namespace RecCode
{

    public class RecogBarcode
    {
        public Mat BarcodeRegion(Mat src_)
        {
            //Cv2.Resize(src, src, new Size(src.Size().Width / 2, src.Size().Height / 2));
            Mat src = src_.Clone();
            Cv2.CvtColor(src, src, ColorConversionCodes.RGB2GRAY);
            Cv2.GaussianBlur(src, src, new Size(3, 3), 0);
            Mat img_X = new Mat();
            Mat img_Y = new Mat();
            Cv2.Sobel(src, img_X, MatType.CV_16S, 1, 0);
            Cv2.Sobel(src, img_Y, MatType.CV_16S, 0, 1);

            Cv2.ConvertScaleAbs(img_X, img_X, 1, 0);
            Cv2.ConvertScaleAbs(img_Y, img_Y, 1, 0);

            Mat margin = img_X - img_Y;
            //Cv2.ImShow("img_Y", margin);
            //Cv2.WaitKey();
            Cv2.Resize(margin, margin, new Size(margin.Width * 0.3, margin.Height * 1.5), 0, 0, InterpolationFlags.Area);
            Cv2.Blur(margin, margin, new Size(3, 3));
            Cv2.MedianBlur(margin, margin, 3);

            Mat imgthreshold = new Mat();
            Cv2.Threshold(margin, imgthreshold, 80, 255, ThresholdTypes.Binary);
            //Cv2.AdaptiveThreshold(margin, imgthreshold, 255, AdaptiveThresholdTypes.GaussianC, ThresholdTypes.Binary, 3, -1);
            Cv2.ImShow("thresh", imgthreshold);
            Cv2.WaitKey();

            //先在水平方向上膨胀，填充条码中间的空隙
            Mat element = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(5, 1));
            Cv2.MorphologyEx(imgthreshold, imgthreshold, MorphTypes.Dilate, element);
            //在垂直方向上腐蚀，分离条码和字符
            element = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(1,5));
            Cv2.MorphologyEx(imgthreshold, imgthreshold, MorphTypes.Erode, element);

            //去除字符
            element = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(10, 10));
            Cv2.MorphologyEx(imgthreshold, imgthreshold, MorphTypes.Open, element);
            Cv2.MorphologyEx(imgthreshold, imgthreshold, MorphTypes.Close, element);
            

            element = Cv2.GetStructuringElement(MorphShapes.Cross, new Size(10, 10));
            Cv2.Erode(imgthreshold, imgthreshold, element);
            Cv2.Erode(imgthreshold, imgthreshold, element);
            Cv2.Dilate(imgthreshold, imgthreshold, element);
            Cv2.Resize(imgthreshold, imgthreshold, new Size(src.Width, src.Height), 0, 0, InterpolationFlags.Area);
            Cv2.ImShow("thresh", imgthreshold);
            Cv2.WaitKey();

            return imgthreshold;


            //计算每个区域的最大内接矩，然后算其包含图像的黑白区域比例

            //Cv2.Dilate(imgthreshold, imgthreshold, element);
            


        }


        public List<RotatedRect> GetBarcode(Mat src)
        {
            Mat imgthresh = BarcodeRegion(src);

            Point[][] contours;
            HierarchyIndex[] hierarchy;
            Cv2.FindContours(imgthresh, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxNone, new Point(0, 0));

            //Cv2.ImShow("X", imgthresh);
            //Cv2.WaitKey();

            List<RotatedRect> barcode = new List<RotatedRect>();
            for (int i=0;i<contours.Length;i++)
            {
                double area = Cv2.ContourArea(contours[i]);
                RotatedRect rect = Cv2.MinAreaRect(contours[i]);
                double ratio = area / (rect.Size.Width * rect.Size.Height);
                if (1-ratio<0.2 && area>200)
                {
                    Cv2.DrawContours(src, contours, i, 255, -1);
                    barcode.Add(rect);
                }
            }

            //RotatedRect p;
            barcode = barcode.OrderBy(p => p.Center.X).ThenBy(p => p.Center.Y).ToList();

            Cv2.ImShow("X", src);
            Cv2.WaitKey();
            return barcode;

        }

        public bool MatchBarcode(Mat target, Mat template)
        {
            bool matched;
            List<RotatedRect> target_barcode = GetBarcode(target);
            List<RotatedRect> template_barcode = GetBarcode(template);

            if (target_barcode.Count != template_barcode.Count)
                return false;
            //如果两个版式相同，每一组偏移量的相差应该是不大的，需要将target翻转一次匹配，因为不知道哪一边是正面
            bool matched_front = JudgeBarcodeLocation(target_barcode, template_barcode);
            //反面
            List<RotatedRect> target_barcode_reverse = target_barcode.OrderBy(p => target.Width - p.Center.X).ThenBy(p => target.Height - p.Center.Y).ToList();
            bool matched_reverse = JudgeBarcodeLocation(target_barcode_reverse, template_barcode);
            matched = matched_front || matched_reverse;

            return matched;
        }

        private bool JudgeBarcodeLocation(List<RotatedRect> target_barcode, List<RotatedRect> template_barcode)
        {
            bool matched = true;

            double height_bias0 = template_barcode[0].Center.Y - target_barcode[0].Center.Y;
            double width_bias0 = template_barcode[0].Center.X - target_barcode[0].Center.X;

            for (int i = 1; i < target_barcode.Count; i++)
            {
                double height_bias = template_barcode[i].Center.Y - target_barcode[i].Center.Y;
                double width_bias = template_barcode[i].Center.X - target_barcode[i].Center.X;

                double ratio_height_bias = Math.Abs(height_bias - height_bias0);
                double ratio_width_bias = Math.Abs(width_bias - width_bias0);

                if (ratio_height_bias > 200 || ratio_width_bias > 200)
                {
                    matched = false;
                    break;
                }
            }
            return matched;
        }

        //public void rec(Mat src)
        //{
        //    System.Drawing.Image img = BitmapConverter.ToBitmap(src);
        //    ImageScanner scanner = new ImageScanner();
        //    List<Symbol> symbols = new List<Symbol>();
        //    symbols = scanner.Scan(img);
        //    if (symbols!=null && symbols.Count>0)
        //    {
        //        string result = null;
        //        symbols.ForEach(s => result += "条码内容:" + s.Data + " 条码质量:" + s.Quality + Environment.NewLine);
        //    }
        //}
    }

}
