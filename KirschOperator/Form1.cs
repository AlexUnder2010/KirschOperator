using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KirschOperator
{
    public partial class Form1 : Form
    {
        private Bitmap _originalBitmap;
        private Bitmap _previewBitmap;
        private Bitmap _resultBitmap;

        public Form1()
        {
            InitializeComponent();
        }

        public static double[,] Kirsch3X3Horizontal => new double[,]
        {
            {5, 5, 5,},
            {-3, 0, -3,},
            {-3, -3, -3,},
        };

        public static double[,] Kirsch3X3Vertical => new double[,]
        {
            {-3, -3, 5,},
            {-3, 0, 5,},
            {-3, -3, 5,},
        };

        private void LoadButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Select an image file.";
            ofd.Filter = "Png Images(*.png)|*.png|Jpeg Images(*.jpg)|*.jpg|Bitmap Images(*.bmp)|*.bmp";

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                StreamReader streamReader = new StreamReader(ofd.FileName);
                _originalBitmap = (Bitmap)Image.FromStream(streamReader.BaseStream);
                streamReader.Close();

                _previewBitmap = _originalBitmap.CopyToSquareCanvas(pictureBox1.Width);
                pictureBox1.Image = _previewBitmap;

                ApplyFilter();
            }
        }
        private void SaveButton_Click(object sender, EventArgs e)
        {
            if (_resultBitmap != null)
            {


                SaveFileDialog sfd = new SaveFileDialog();
                sfd.Title = "Path";
                sfd.Filter = "Png Images(*.png)|*.png|Jpeg Images(*.jpg)|*.jpg|Bitmap Images(*.bmp)|*.bmp";

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var extension = Path.GetExtension(sfd.FileName);
                    if (extension != null)
                    {
                        string fileExtension = extension.ToUpper();
                        var imgFormat = ImageFormat.Png;

                        switch (fileExtension)
                        {
                            case "BMP":
                                imgFormat = ImageFormat.Bmp;
                                break;
                            case "JPG":
                                imgFormat = ImageFormat.Jpeg;
                                break;
                        }

                        var streamWriter = new StreamWriter(sfd.FileName, false);
                        _resultBitmap.Save(streamWriter.BaseStream, imgFormat);
                        streamWriter.Flush();
                        streamWriter.Close();
                    }

                    _resultBitmap = null;
                }
            }
        }
        private void ApplyFilter()
        {
            var selectedSource = _previewBitmap;
            var bitmapResult = selectedSource.KirschFilter();
            if (bitmapResult != null)
            {
                pictureBox1.Image = bitmapResult;
                _resultBitmap = bitmapResult;
            }
        }
    }
    public static class ExtBitmap
    {

        public static Bitmap CopyToSquareCanvas(this Bitmap sourceBitmap, int canvasWidthLenght)
        {
            var maxSide = sourceBitmap.Width > sourceBitmap.Height
                ? sourceBitmap.Width
                : sourceBitmap.Height;

            var ratio = maxSide / (float)canvasWidthLenght;

            var bitmapResult = sourceBitmap.Width > sourceBitmap.Height
                ? new Bitmap(canvasWidthLenght, (int)(sourceBitmap.Height / ratio))
                : new Bitmap((int)(sourceBitmap.Width / ratio), canvasWidthLenght);

            using (var graphicsResult = Graphics.FromImage(bitmapResult))
            {
                graphicsResult.CompositingQuality = CompositingQuality.HighQuality;
                graphicsResult.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphicsResult.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphicsResult.DrawImage(sourceBitmap,
                    new Rectangle(0, 0,
                        bitmapResult.Width, bitmapResult.Height),
                    new Rectangle(0, 0,
                        sourceBitmap.Width, sourceBitmap.Height),
                    GraphicsUnit.Pixel);
                graphicsResult.Flush();
            }

            return bitmapResult;
        }

        private static Bitmap ConvolutionFilter(this Bitmap sourceBitmap,
            double[,] xFilterMatrix,
            double[,] yFilterMatrix,
            bool grayscale = false)
        {
            var sourceData = sourceBitmap.LockBits(new Rectangle(0, 0,
                    sourceBitmap.Width, sourceBitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            var pixelBuffer = new byte[sourceData.Stride * sourceData.Height];
            var resultBuffer = new byte[sourceData.Stride * sourceData.Height];

            Marshal.Copy(sourceData.Scan0, pixelBuffer, 0, pixelBuffer.Length);
            sourceBitmap.UnlockBits(sourceData);

            if (grayscale)
            {
                for (var k = 0; k < pixelBuffer.Length; k += 4)
                {
                    var rgb = pixelBuffer[k] * 0.11f;
                    rgb += pixelBuffer[k + 1] * 0.59f;
                    rgb += pixelBuffer[k + 2] * 0.3f;

                    pixelBuffer[k] = (byte)rgb;
                    pixelBuffer[k + 1] = pixelBuffer[k];
                    pixelBuffer[k + 2] = pixelBuffer[k];
                    pixelBuffer[k + 3] = 255;
                }
            }

            var filterOffset = 1;

            for (var offsetY = filterOffset;
                offsetY <
                sourceBitmap.Height - filterOffset;
                offsetY++)
                for (var offsetX = filterOffset;
                    offsetX <
                    sourceBitmap.Width - filterOffset;
                    offsetX++)
                {
                    double greenX;
                    double redX;
                    var blueX = greenX = redX = 0;
                    double greenY;
                    double redY;
                    var blueY = greenY = redY = 0;

                    var byteOffset = offsetY *
                                     sourceData.Stride +
                                     offsetX * 4;

                    for (var filterY = -filterOffset;
                        filterY <= filterOffset;
                        filterY++)
                        for (var filterX = -filterOffset;
                            filterX <= filterOffset;
                            filterX++)
                        {
                            var calcOffset = byteOffset +
                                             filterX * 4 +
                                             filterY * sourceData.Stride;

                            blueX += pixelBuffer[calcOffset] *
                                     xFilterMatrix[filterY + filterOffset,
                                         filterX + filterOffset];

                            greenX += pixelBuffer[calcOffset + 1] *
                                      xFilterMatrix[filterY + filterOffset,
                                          filterX + filterOffset];

                            redX += pixelBuffer[calcOffset + 2] *
                                    xFilterMatrix[filterY + filterOffset,
                                        filterX + filterOffset];

                            blueY += pixelBuffer[calcOffset] *
                                     yFilterMatrix[filterY + filterOffset,
                                         filterX + filterOffset];

                            greenY += pixelBuffer[calcOffset + 1] *
                                      yFilterMatrix[filterY + filterOffset,
                                          filterX + filterOffset];

                            redY += pixelBuffer[calcOffset + 2] *
                                    yFilterMatrix[filterY + filterOffset,
                                        filterX + filterOffset];
                        }

                    var blueTotal = Math.Sqrt(blueX * blueX + blueY * blueY);
                    var greenTotal = Math.Sqrt(greenX * greenX + greenY * greenY);
                    var redTotal = Math.Sqrt(redX * redX + redY * redY);

                    if (blueTotal > 255)
                        blueTotal = 255;
                    else if (blueTotal < 0)
                        blueTotal = 0;

                    if (greenTotal > 255)
                        greenTotal = 255;
                    else if (greenTotal < 0)
                        greenTotal = 0;

                    if (redTotal > 255)
                        redTotal = 255;
                    else if (redTotal < 0)
                        redTotal = 0;

                    resultBuffer[byteOffset] = (byte)blueTotal;
                    resultBuffer[byteOffset + 1] = (byte)greenTotal;
                    resultBuffer[byteOffset + 2] = (byte)redTotal;
                    resultBuffer[byteOffset + 3] = 255;
                }

            var resultBitmap = new Bitmap(sourceBitmap.Width, sourceBitmap.Height);

            var resultData = resultBitmap.LockBits(new Rectangle(0, 0,
                    resultBitmap.Width, resultBitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format32bppArgb);

            Marshal.Copy(resultBuffer, 0, resultData.Scan0, resultBuffer.Length);
            resultBitmap.UnlockBits(resultData);

            return resultBitmap;
        }
        
        public static Bitmap KirschFilter(this Bitmap sourceBitmap,
            bool grayscale = true)
        {
            var resultBitmap = sourceBitmap.ConvolutionFilter(Form1.Kirsch3X3Horizontal,
                Form1.Kirsch3X3Vertical, grayscale);

            return resultBitmap;
        }
    }
}
