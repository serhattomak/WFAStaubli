using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Emgu.CV.Util;

namespace WFAStaubli
{
    public partial class Form1 : Form
    {
        private double scaleFactor = 0.5;
        private double marginX = 10; // Margin from the left
        private double marginY = 10; // Margin from the top

        public Form1()
        {
            InitializeComponent();
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Image files (*.jpg, *.jpeg, *.png) | *.jpg; *.jpeg; *.png",
                Title = "Select an Image"
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                // Load and display the image in the PictureBox
                pcbOriginal.Image = new Bitmap(openFileDialog.FileName);
                pcbOriginal.SizeMode = PictureBoxSizeMode.StretchImage;
            }
        }

        private void btnDraw_Click(object sender, EventArgs e)
        {
            if (pcbConverted.Image != null)
            {
                Bitmap convertedImage = pcbConverted.Image as Bitmap;

                if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                {
                    MessageBox.Show("Converted image is empty or not a Bitmap.");
                    return;
                }

                var pixelCoordinates = ExtractPixelCoordinates(convertedImage);
                List<string> commands = GenerateRobotCommandsFromPixels(pixelCoordinates);

                DrawRobotCommands(commands);
            }
        }

        private void btnConvert_Click(object sender, EventArgs e)
        {
            if (pcbOriginal.Image != null)
            {
                Bitmap originalImage = new Bitmap(pcbOriginal.Image);
                if (originalImage.Width == 0 || originalImage.Height == 0)
                {
                    MessageBox.Show("Loaded image is empty.");
                    return;
                }

                Bitmap convertedImage = ConvertToBlackAndWhite(originalImage);
                pcbConverted.Image = convertedImage;
                pcbConverted.SizeMode = PictureBoxSizeMode.StretchImage;
            }
        }

        private Bitmap ConvertToBlackAndWhite(Bitmap originalImage)
        {
            // Convert Bitmap to Image<Bgr, byte>
            Image<Bgr, byte> imgBgr;

            if (originalImage.PixelFormat == System.Drawing.Imaging.PixelFormat.Format32bppArgb)
            {
                // Handle transparency by converting transparent pixels to white
                imgBgr = new Image<Bgr, byte>(originalImage.Width, originalImage.Height);

                for (int y = 0; y < originalImage.Height; y++)
                {
                    for (int x = 0; x < originalImage.Width; x++)
                    {
                        Color pixelColor = originalImage.GetPixel(x, y);
                        if (pixelColor.A < 255)
                        {
                            imgBgr.Data[y, x, 0] = 255; // Blue
                            imgBgr.Data[y, x, 1] = 255; // Green
                            imgBgr.Data[y, x, 2] = 255; // Red
                        }
                        else
                        {
                            imgBgr.Data[y, x, 0] = pixelColor.B;
                            imgBgr.Data[y, x, 1] = pixelColor.G;
                            imgBgr.Data[y, x, 2] = pixelColor.R;
                        }
                    }
                }
            }
            else
            {
                imgBgr = originalImage.ToImage<Bgr, byte>();
            }

            // Convert to grayscale
            Mat matImage = new Mat();
            CvInvoke.CvtColor(imgBgr.Mat, matImage, ColorConversion.Bgr2Gray);
            CvInvoke.Threshold(matImage, matImage, 128, 255, ThresholdType.Binary);

            int size = 2;
            Mat element = CvInvoke.GetStructuringElement(ElementShape.Rectangle, new Size(size, size), new Point(-1, -1));
            CvInvoke.Erode(matImage, matImage, element, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));
            CvInvoke.Dilate(matImage, matImage, element, new Point(-1, -1), 1, BorderType.Default, default(MCvScalar));

            Console.WriteLine("Image converted to black and white.");
            return matImage.ToBitmap();
        }

        private List<Point> ExtractPixelCoordinates(Bitmap image)
{
    List<Point> pixelCoordinates = new List<Point>();

    for (int y = 0; y < image.Height; y++)
    {
        for (int x = 0; x < image.Width; x++)
        {
            Color pixelColor = image.GetPixel(x, y);
            if (pixelColor.R == 0) // Assuming black pixel is part of the shape
            {
                pixelCoordinates.Add(new Point(x, y));
            }
        }
    }

    return pixelCoordinates.OrderBy(p => p.Y).ThenBy(p => p.X).ToList(); // Sort by Y first, then by X to maintain order
}


        private List<string> GenerateRobotCommandsFromPixels(List<Point> pixelCoordinates)
        {
            List<string> commands = new List<string>();

            double safeHeight = 0;
            double drawHeight = 20;
            bool isFirstCommand = true;
            double lastX = 0, lastY = 0;

            double[] defaultOrientation = new double[3];

            // Determine the minimum X and Y values
            double minX = double.MaxValue;
            double minY = double.MaxValue;

            foreach (var point in pixelCoordinates)
            {
                if (point.X < minX)
                    minX = point.X;
                if (point.Y < minY)
                    minY = point.Y;
            }

            commands.Add(FormatCommand("movej", new Point(0, 0), defaultOrientation, safeHeight));

            foreach (var point in pixelCoordinates)
            {
                var (robotX, robotY) = DefinePoint(point.X - minX, point.Y - minY, scaleFactor, marginX, marginY);

                Point robotPoint = new Point((int)robotX, (int)robotY);
                double[] orientation = defaultOrientation;

                if (isFirstCommand || (Math.Abs(robotX - lastX) > 20 || Math.Abs(robotY - lastY) > 20))
                {
                    if (!isFirstCommand)
                    {
                        // Lift to safe height
                        commands.Add(FormatCommand("movel", new Point((int)lastX, (int)lastY), orientation, safeHeight));
                        commands.Add("waitEndMove()");
                    }

                    // Move to the new position at safe height
                    commands.Add(FormatCommand("movej", robotPoint, orientation, safeHeight));
                    commands.Add("waitEndMove()");

                    // Move down to draw height
                    commands.Add(FormatCommand("movel", robotPoint, orientation, drawHeight));
                }

                // Draw the current point
                commands.Add(FormatCommand("movel", robotPoint, orientation, drawHeight));

                lastX = robotX;
                lastY = robotY;
                isFirstCommand = false;
            }

            // Lift to safe height at the end
            commands.Add("waitEndMove()");
            commands.Add(FormatCommand("movel", new Point((int)lastX, (int)lastY), defaultOrientation, safeHeight));

            Console.WriteLine($"Total commands generated: {commands.Count}");
            return commands;
        }

        private string FormatCommand(string commandType, Point point, double[] orientation, double zValue)
        {
            return $"{commandType}(appro(pPoint1,{{ {point.X}, {point.Y}, {zValue}, {orientation[0]}, {orientation[1]}, {orientation[2]} }}), tTool, mFast)";
        }

        private (double, double) DefinePoint(double imageX, double imageY, double scaleFactor, double marginX, double marginY)
        {
            double robotX = imageX * scaleFactor + marginX;
            double robotY = imageY * scaleFactor + marginY;
            return (robotX, robotY);
        }

        private void btnCommand_Click(object sender, EventArgs e)
        {
            try
            {
                if (pcbConverted.Image != null)
                {
                    Bitmap convertedImage = pcbConverted.Image as Bitmap;

                    if (convertedImage == null || convertedImage.Width == 0 || convertedImage.Height == 0)
                    {
                        MessageBox.Show("Converted image is empty or not a Bitmap.");
                        return;
                    }

                    var pixelCoordinates = ExtractPixelCoordinates(convertedImage);

                    SaveFileDialog saveFileDialog = new SaveFileDialog
                    {
                        Filter = "Text Files (*.txt)|*.txt|PGX Files (*.pgx)|*.pgx",
                        Title = "Save Robot Commands"
                    };

                    if (saveFileDialog.ShowDialog() == DialogResult.OK)
                    {
                        List<string> commands = GenerateRobotCommandsFromPixels(pixelCoordinates);
                        if (saveFileDialog.FileName.EndsWith(".txt"))
                        {
                            SaveCommandsAsText(saveFileDialog.FileName, commands);
                        }
                        else if (saveFileDialog.FileName.EndsWith(".pgx"))
                        {
                            SaveCommandsAsXml(saveFileDialog.FileName, commands);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred: {ex.Message}");
            }
        }

        private void SaveCommandsAsText(string filePath, List<string> commands)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("begin");
                sw.WriteLine("cls()");
                sw.WriteLine("close(tTool)");
                sw.WriteLine("open(tTool1)");
                sw.WriteLine("nError=setFrame(pOrigin,pX,pY,fMasa)");
                foreach (string command in commands)
                {
                    sw.WriteLine(command);
                }
                sw.WriteLine("waitEndMove()");
                sw.WriteLine("end");
            }
        }

        private void SaveCommandsAsXml(string filePath, List<string> commands)
        {
            using (StreamWriter sw = new StreamWriter(filePath))
            {
                sw.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
                sw.WriteLine("<Programs xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns=\"http://www.staubli.com/robotics/VAL3/Program/2\">");
                sw.WriteLine("  <Program name=\"start\">");
                sw.WriteLine("    <Code><![CDATA[");
                sw.WriteLine("begin");
                sw.WriteLine("cls()");
                sw.WriteLine("close(tTool)");
                sw.WriteLine("open(tTool1)");
                sw.WriteLine("nError=setFrame(pOrigin,pX,pY,fMasa)");
                foreach (string command in commands)
                {
                    sw.WriteLine(command);
                }
                sw.WriteLine("waitEndMove()");
                sw.WriteLine("end");
                sw.WriteLine("]]></Code>");
                sw.WriteLine("  </Program>");
                sw.WriteLine("</Programs>");
            }
        }

        private void DrawRobotCommands(List<string> commands)
        {
            Bitmap drawingImage = new Bitmap(pcbDrawing.Width, pcbDrawing.Height);
            using (Graphics g = Graphics.FromImage(drawingImage))
            {
                g.Clear(Color.White);
                Pen pen = new Pen(Color.Black, 2);

                Point? lastDrawingPoint = null;
                bool inDrawing = false;

                foreach (var command in commands)
                {
                    if (command.StartsWith("movej") || command.StartsWith("movel"))
                    {
                        Point point = ParsePointFromCommand(command);
                        if (command.StartsWith("movej"))
                        {
                            if (inDrawing)
                            {
                                g.DrawLine(pen, lastDrawingPoint.Value, point);
                                inDrawing = false;
                            }
                            lastDrawingPoint = point;
                        }
                        else if (command.StartsWith("movel"))
                        {
                            if (lastDrawingPoint.HasValue)
                            {
                                g.DrawLine(pen, lastDrawingPoint.Value, point);
                            }
                            lastDrawingPoint = point;
                            inDrawing = true;
                        }
                    }
                    else if (command.StartsWith("waitEndMove()"))
                    {
                        inDrawing = false;
                    }
                }
            }

            pcbDrawing.Image = drawingImage;
            pcbDrawing.SizeMode = PictureBoxSizeMode.StretchImage;
        }

        private Point ParsePointFromCommand(string command)
        {
            var start = command.IndexOf('{') + 1;
            var end = command.IndexOf('}');
            var parts = command.Substring(start, end - start).Split(',');
            int x = int.Parse(parts[0].Trim());
            int y = int.Parse(parts[1].Trim());
            return new Point(x, y);
        }
    }
}
