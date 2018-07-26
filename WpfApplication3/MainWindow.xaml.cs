using AForge;
using AForge.Imaging;
using AForge.Math.Geometry;
using ImageMagick;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace WpfApplication3
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Checkmarkresults resultsWindow = new Checkmarkresults();
    
        List<bool> isChecked = new List<bool>();
        List<String> CheckBoxName = new List<String>();
        List<int> cAverageValue = new List<int>();

        public MainWindow()
        {
            InitializeComponent();
        }
        
        private void ImgBase_MouseDown(object sender, MouseButtonEventArgs e)
        {
            label.Visibility = Visibility.Collapsed;

            OpenFileDialog openFileDialog1 = new OpenFileDialog
            {
                Filter = "Portable Network Graphics|*.png",
                Title = "Select a picture"
            };

            if (openFileDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                Bitmap myBitmap = new Bitmap(openFileDialog1.OpenFile());
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Wait;
                myBitmap.Save(Path.GetTempPath()+"temp.bmp");
                Process.Start(Path.GetTempPath() + "temp.bmp");
                Bitmap bitmapToDisort = new Bitmap(myBitmap);
                imgBase.Source = BitmapToImageSource(myBitmap);

                myBitmap = EdgeDetect(myBitmap, float.Parse(tbEdgeDetectBlurRadius.Text), float.Parse(tbEdgeDetectBlurSigma.Text));
                imgEdgeDetection.Source = BitmapToImageSource(myBitmap);

                var results = ShapeCheck(myBitmap);
                myBitmap = results.Item1;
                imgShapeCheck.Source = BitmapToImageSource(myBitmap);

                myBitmap = DistortImage(bitmapToDisort, results.Item2, results.Item3, results.Item4, results.Item5);

                myBitmap = CheckCheckbox(myBitmap);
                imgCheckmarks.Source = BitmapToImageSource(myBitmap);

                resultsWindow.Owner = this;
                resultsWindow.Show();

                CheckBoxName.Add("Diabetes mellitus");
                CheckBoxName.Add("Mehrlingsschwangerschaft");
                CheckBoxName.Add("Gestationsdiabetes");
                CheckBoxName.Add("(Poly-)Hydramnion");
                CheckBoxName.Add("Dauermedikation");
                CheckBoxName.Add("Oligohydramnion");
                CheckBoxName.Add("akute oder chronische Infektionen");
                CheckBoxName.Add("besondere psychische Belastungen");
                CheckBoxName.Add("besondere soziale Belastung");
                CheckBoxName.Add("Antikörper-Suchtest positiv");
                CheckBoxName.Add("Abusus");
                CheckBoxName.Add("B-Streptokokken-Status der Mutter");

                for(int i = 0; i < CheckBoxName.Count; i++)
                {
                    resultsWindow.listView.Items.Add(new MyItem { Checked = isChecked[i], Name = CheckBoxName[i], cAverage = cAverageValue[i] });
                }

                CheckInResultWindow();
                Mouse.OverrideCursor = System.Windows.Input.Cursors.Arrow;
            }
        }

        public Bitmap EdgeDetect(Bitmap myBitmap, float r, float s)
        {
            using (MagickImage image = new MagickImage(myBitmap))
            {
                image.ReduceNoise();
                image.Blur(r, s);
                image.CannyEdge();

                return image.ToBitmap();
            }
        }

        private Tuple<Bitmap, IntPoint, IntPoint, IntPoint, IntPoint> ShapeCheck(Bitmap image)
        {
            IntPoint p1 = new IntPoint();
            IntPoint p2 = new IntPoint();
            IntPoint p3 = new IntPoint();
            IntPoint p4 = new IntPoint();

            BlobCounter blobCounter = new BlobCounter();

            blobCounter.FilterBlobs = true;
            blobCounter.MinHeight = 500;
            blobCounter.MinWidth = 400;
            blobCounter.ProcessImage(image);
            Blob[] blobs = blobCounter.GetObjectsInformation();

            SimpleShapeChecker shapeChecker = new SimpleShapeChecker();

            foreach (var blob in blobs)
            {
                List<IntPoint> edgePoints = blobCounter.GetBlobsEdgePoints(blob);
                List<IntPoint> cornerPoints;

                if (shapeChecker.IsQuadrilateral(edgePoints, out cornerPoints))
                {
                    if (shapeChecker.CheckPolygonSubType(cornerPoints) == PolygonSubType.Rectangle)
                    {
                        var points = SortCorners(cornerPoints, image);
                        p1 = points.Item1;
                        p2 = points.Item2;
                        p3 = points.Item3;
                        p4 = points.Item4;

                        lblInfo.Content = "p1: " + p1.ToString() + " | p2: " + p2.ToString() + " | p3: " + p3.ToString() + " | p4:  " + p4.ToString();
                        List<IntPoint> Points = new List<IntPoint>();

                        foreach (var point in cornerPoints)
                        {
                            Points.Add(new IntPoint(point.X, point.Y));
                        }

                        Graphics g = Graphics.FromImage(image);
                        g.DrawPolygon(new Pen(Color.Red, 5.0f), Points.Select(p => new System.Drawing.Point(p.X, p.Y)).ToArray());
                    }
                }
            }

            return Tuple.Create(image, p1, p2, p3, p4);
        }

        public Tuple<IntPoint, IntPoint, IntPoint, IntPoint> SortCorners(List<IntPoint> cornerPoints, Bitmap image)
        {
            IntPoint topLeft = new IntPoint(0, 0);
            IntPoint topRight = new IntPoint(image.Width, 0);
            IntPoint bottomLeft = new IntPoint(0, image.Height);
            IntPoint bottomRight = new IntPoint(image.Width, image.Height);
            IntPoint p1 = new IntPoint(0, 0);
            IntPoint p2 = new IntPoint(0, 0);
            IntPoint p3 = new IntPoint(0, 0);
            IntPoint p4 = new IntPoint(0, 0);

            if (GetDistance(topLeft, cornerPoints[0]) < GetDistance(topLeft, cornerPoints[1]) &&
                GetDistance(topLeft, cornerPoints[0]) < GetDistance(topLeft, cornerPoints[2]) &&
                GetDistance(topLeft, cornerPoints[0]) < GetDistance(topLeft, cornerPoints[3]))
            {
                p1 = cornerPoints[0];
            }
            else if (GetDistance(topLeft, cornerPoints[1]) < GetDistance(topLeft, cornerPoints[2]) &&
                GetDistance(topLeft, cornerPoints[1]) < GetDistance(topLeft, cornerPoints[3]) &&
                GetDistance(topLeft, cornerPoints[1]) < GetDistance(topLeft, cornerPoints[0]))
            {
                p1 = cornerPoints[1];
            }
            else if (GetDistance(topLeft, cornerPoints[2]) < GetDistance(topLeft, cornerPoints[3]) &&
                GetDistance(topLeft, cornerPoints[2]) < GetDistance(topLeft, cornerPoints[0]) &&
                GetDistance(topLeft, cornerPoints[2]) < GetDistance(topLeft, cornerPoints[1]))
            {
                p1 = cornerPoints[2];
            }
            else
            {
                p1 = cornerPoints[3];
            }

            //oben rechts
            if (GetDistance(topRight, cornerPoints[0]) < GetDistance(topRight, cornerPoints[1]) &&
                GetDistance(topRight, cornerPoints[0]) < GetDistance(topRight, cornerPoints[2]) &&
                GetDistance(topRight, cornerPoints[0]) < GetDistance(topRight, cornerPoints[3]))
            {
                p2 = cornerPoints[0];
            }
            else if (GetDistance(topRight, cornerPoints[1]) < GetDistance(topRight, cornerPoints[2]) &&
                GetDistance(topRight, cornerPoints[1]) < GetDistance(topRight, cornerPoints[3]) &&
                GetDistance(topRight, cornerPoints[1]) < GetDistance(topRight, cornerPoints[0]))
            {
                p2 = cornerPoints[1];
            }
            else if (GetDistance(topRight, cornerPoints[2]) < GetDistance(topRight, cornerPoints[3]) &&
                GetDistance(topRight, cornerPoints[2]) < GetDistance(topRight, cornerPoints[0]) &&
                GetDistance(topRight, cornerPoints[2]) < GetDistance(topRight, cornerPoints[1]))
            {
                p2 = cornerPoints[2];
            }
            else
            {
                p2 = cornerPoints[3];
            }

            //unten links
            if (GetDistance(bottomLeft, cornerPoints[0]) < GetDistance(bottomLeft, cornerPoints[1]) &&
                GetDistance(bottomLeft, cornerPoints[0]) < GetDistance(bottomLeft, cornerPoints[2]) &&
                GetDistance(bottomLeft, cornerPoints[0]) < GetDistance(bottomLeft, cornerPoints[3]))
            {
                p3 = cornerPoints[0];
            }
            else if (GetDistance(bottomLeft, cornerPoints[1]) < GetDistance(bottomLeft, cornerPoints[2]) &&
                GetDistance(bottomLeft, cornerPoints[1]) < GetDistance(bottomLeft, cornerPoints[3]) &&
                GetDistance(bottomLeft, cornerPoints[1]) < GetDistance(bottomLeft, cornerPoints[0]))
            {
                p3 = cornerPoints[1];
            }
            else if (GetDistance(bottomLeft, cornerPoints[2]) < GetDistance(bottomLeft, cornerPoints[3]) &&
                GetDistance(bottomLeft, cornerPoints[2]) < GetDistance(bottomLeft, cornerPoints[0]) &&
                GetDistance(bottomLeft, cornerPoints[2]) < GetDistance(bottomLeft, cornerPoints[1]))
            {
                p3 = cornerPoints[2];
            }
            else
            {
                p3 = cornerPoints[3];
            }

            //unten rechts
            if (GetDistance(bottomRight, cornerPoints[0]) < GetDistance(bottomRight, cornerPoints[1]) &&
                GetDistance(bottomRight, cornerPoints[0]) < GetDistance(bottomRight, cornerPoints[2]) &&
                GetDistance(bottomRight, cornerPoints[0]) < GetDistance(bottomRight, cornerPoints[3]))
            {
                p4 = cornerPoints[0];
            }
            else if (GetDistance(bottomRight, cornerPoints[1]) < GetDistance(bottomRight, cornerPoints[2]) &&
                GetDistance(bottomRight, cornerPoints[1]) < GetDistance(bottomRight, cornerPoints[3]) &&
                GetDistance(bottomRight, cornerPoints[1]) < GetDistance(bottomRight, cornerPoints[0]))
            {
                p4 = cornerPoints[1];
            }
            else if (GetDistance(bottomRight, cornerPoints[2]) < GetDistance(bottomRight, cornerPoints[3]) &&
                GetDistance(bottomRight, cornerPoints[2]) < GetDistance(bottomRight, cornerPoints[0]) &&
                GetDistance(bottomRight, cornerPoints[2]) < GetDistance(bottomRight, cornerPoints[1]))
            {
                p4 = cornerPoints[2];
            }
            else
            {
                p4 = cornerPoints[3];
            }

            return Tuple.Create(p1, p2, p3, p4);
        }

        public double GetDistance(IntPoint p1, IntPoint p2)
        {
            double distance = Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2));

            return distance;
        }

        private Bitmap DistortImage(Bitmap bitmap, IntPoint p1, IntPoint p2, IntPoint p3, IntPoint p4)
        {
            if (p1.X != 0 && p2.Y != 0 && p3.Y != 0 && p4.X != 0)
            {
                using (MagickImage image = new MagickImage(bitmap))
                {
                    if (cbNormalize.IsChecked ?? true)
                    {
                        image.Normalize();
                    }

                    if (cbAutoGamma.IsChecked ?? true)
                    {
                        image.AutoGamma();
                    }

                    if (cbAutoLevel.IsChecked ?? true)
                    {
                        image.AutoLevel();
                    }

                    image.Threshold(new Percentage(int.Parse(tbThreshold.Text)));
                    image.Distort(DistortMethod.Perspective, new double[] {
                        p1.X, p1.Y, 0, 0,
                        p2.X, p2.Y, image.Width, 0,
                        p3.X, p3.Y, 0, image.Height,
                        p4.X, p4.Y, image.Width, image.Height
                    });

                    MagickGeometry size = new MagickGeometry(561, 795)
                    {
                        IgnoreAspectRatio = true
                    };

                    image.Resize(size);
                    //image.Write("C:\\Users\\RLabonde\\Desktop\\test\\test.bmp");
                    
                    return image.ToBitmap();
                }
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("No point was detected!", "No points", MessageBoxButtons.OK, MessageBoxIcon.Error);

                return bitmap;
            }
        }

        private Bitmap CheckCheckbox(Bitmap image)
        {
            //Mittelpunkt der Checkmarks
            IntPoint[] points = new IntPoint[] {
                new IntPoint { X = 75, Y = 175 },
                new IntPoint { X = 300, Y = 175 },
                new IntPoint { X = 75, Y = 191 },
                new IntPoint { X = 300, Y = 191 },
                new IntPoint { X = 75, Y = 207 },
                new IntPoint { X = 300, Y = 207 },
                new IntPoint { X = 75, Y = 223 },
                new IntPoint { X = 300, Y = 223 },
                new IntPoint { X = 300, Y = 239 },
                new IntPoint { X = 75, Y = 255 },
                new IntPoint { X = 300, Y = 255 },
                new IntPoint { X = 75, Y = 271 },
            };

            int checkBoxID = 0;

            foreach (IntPoint p in points)
            {
                IntPoint middle = new IntPoint
                {
                    X = p.X,
                    Y = p.Y
                };

                int halfBoxSize = int.Parse(tbCheckBoxSize.Text) / 2;

                IntPoint topLeft = new IntPoint
                {
                    X = middle.X - halfBoxSize,
                    Y = middle.Y + halfBoxSize
                };

                IntPoint topRight = new IntPoint
                {
                    X = middle.X + halfBoxSize,
                    Y = middle.Y + halfBoxSize
                };

                IntPoint bottomLeft = new IntPoint
                {
                    X = middle.X - halfBoxSize,
                    Y = middle.Y - halfBoxSize
                };

                IntPoint bottomRight = new IntPoint
                {
                    X = middle.X + halfBoxSize,
                    Y = middle.Y - halfBoxSize
                };

                int totalA = 0;

                //bereich um den Kasten
                for (int i = topLeft.X; i < topRight.X; i++)
                {
                    for (int j = bottomLeft.Y; j < topRight.Y; j++)
                    {
                        Color c = image.GetPixel(i, j);
                        //Farbwerte im Feld addieren
                        totalA = totalA + c.A;

                        if (cbShowCheckedArea.IsChecked ?? true)
                        {
                            image.SetPixel(i, j, Color.FromArgb(255, 255,0,0));
                        }

                    }
                }

                //Totalen Farbwert durch Anzahl an Pixeln teilen 
                int cAverage = totalA / ((halfBoxSize * 2) * (halfBoxSize * 2));

                if (cAverage > int.Parse(tbUncheckedUnder.Text) && cAverage < int.Parse(tbCheckedUnder.Text))
                {
                    isChecked.Add(true);
                }
                else
                {
                    isChecked.Add(false);
                }

                cAverageValue.Add(cAverage);
                checkBoxID++;
            }

            return image;
        }

        public class MyItem
        {
            public bool Checked { get; set; }

            public string Name { get; set; }

            public int cAverage { get; set; }
        }

        private void CheckInResultWindow()
        {
            resultsWindow.cbResults1.IsChecked = isChecked[0];
            resultsWindow.cbResults2.IsChecked = isChecked[1];
            resultsWindow.cbResults3.IsChecked = isChecked[2];
            resultsWindow.cbResults4.IsChecked = isChecked[3];
            resultsWindow.cbResults5.IsChecked = isChecked[4];
            resultsWindow.cbResults6.IsChecked = isChecked[5];
            resultsWindow.cbResults7.IsChecked = isChecked[6];
            resultsWindow.cbResults8.IsChecked = isChecked[7];
            resultsWindow.cbResults9.IsChecked = isChecked[8];
            resultsWindow.cbResults10.IsChecked = isChecked[9];
            resultsWindow.cbResults11.IsChecked = isChecked[10];
            resultsWindow.cbResults12.IsChecked = isChecked[11];
        }

        BitmapImage BitmapToImageSource(Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;

                BitmapImage bitmapimage = new BitmapImage();

                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();

                return bitmapimage;
            }
        }
    }
}
