using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using System.IO;
using System.Xml.Linq;
using System.Net;
using System.Drawing.Imaging;
using System.Threading;
using Xabe.FFmpeg;
using AForge.Video;
using Accord.Video.FFMPEG;
using Accord;
using System.Net.Http;
using Accord.IO;

namespace Streeview_player
{
    public partial class Form1 : Form
    {
        private String template = "https://maps.googleapis.com/maps/api/streetview?size=500x500&"+
                                    "location={0},{1}&fov=120&heading={2}&pitch=0&key=AIzaSyDvbqOmEK6OzG5sbaudEeo8E2H5Puyd4vY";
        private String templateMap = "https://maps.googleapis.com/maps/api/staticmap?"+
            "zoom=13&size=306x500&maptype=roadmap&markers=color:red%7Clabel:T%7C{0},{1}&key=AIzaSyDvbqOmEK6OzG5sbaudEeo8E2H5Puyd4vY";
        private string templateAt = "https://maps.googleapis.com/maps/api/elevation/xml?" +
            "locations={0}&key=AIzaSyDvbqOmEK6OzG5sbaudEeo8E2H5Puyd4vY";
        private List<Pos> pos = new List<Pos>();
        private bool[] downloaded;
        private Bitmap[] img;
        private Bitmap[] imgMap;
        private WebClient client = new WebClient();
        private const int R = 6378;
        bool downloading = false;
        HttpClient clientHttp = new HttpClient();
        private class Pos
        {
            public double lat;
            public double lon;
            public double at;
            public double angle;
            public double distance;
            public Pos(double lat, double lon, double at, double dist)
            {
                this.lat = lat;
                this.lon = lon;
                this.at = at;
                distance = dist;
            }

        }
        public Form1()
        {
            InitializeComponent();
            readXML();
            timerPlay.Interval = 1000 / trackBarSpeed.Value;
            trackBar.Value = 0;
        }
        private void readXML()
        {
            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Parse;
            XmlReader reader = XmlReader.Create("C:\\Users\\krato\\Documents\\Google API\\Aktivita-05-07-2021-09-45-14.gpx", settings);
            double min = 10000;
            double max = 0;
            while (reader.Read())
            {
                if (reader.Name.Equals("trkpt"))
                {
                    XElement el = (XElement)XNode.ReadFrom(reader);
                    double at = Double.Parse(el.Elements().Where(item => item.Name.LocalName.Equals("ele")).ToArray()[0].Value);
                    if (at > max) max = at;
                    if (at < min) min = at;
                    elevationChart.Series["Elevation"].Points.AddXY(pos.Count, at);
                    double lat = Double.Parse(el.Attribute("lat").Value);
                    double lon = Double.Parse(el.Attribute("lon").Value);
                    double dist = 0;
                    if (pos.Count > 0)
                    {
                        double deltaLon = lon - pos[pos.Count - 1].lon;
                        double a = Math.Sin(deltaLon) * Math.Cos(lat);
                        double b = Math.Cos(pos[pos.Count - 1].lat) * Math.Sin(lat)-Math.Sin(pos[pos.Count - 1].lat)*Math.Cos(lat)*Math.Cos(deltaLon);
                        //pos[pos.Count - 1].angle = Math.Atan(Math.Abs((lat - pos[pos.Count - 1].lat) / (lon - pos[pos.Count - 1].lon))) * 180 / Math.PI;
                        pos[pos.Count - 1].angle = Math.Atan2(a,b) * 180 / Math.PI;

                        double toRad = Math.PI / 180;
                        double deltaLat = lat - pos[pos.Count - 1].lat;
                        a = Math.Pow(Math.Sin(deltaLat * toRad / 2), 2) + Math.Cos(pos[pos.Count - 1].lat * toRad) * Math.Cos(lat* toRad) * Math.Pow(Math.Sin(deltaLon* toRad / 2),2);
                        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
                        dist = R * c + pos[pos.Count-1].distance;
                    }
                    Pos p = new Pos(lat, lon, at, dist);
                    pos.Add(p);
                }
            }
            elevationChart.ChartAreas[0].AxisY.Minimum = min - 30;
            elevationChart.ChartAreas[0].AxisY.Maximum = max + 30;
            trackBar.Maximum = pos.Count - 1;
            downloaded = new bool[pos.Count];
            img = new Bitmap[pos.Count];
            imgMap = new Bitmap[pos.Count];
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (timerPlay.Enabled)
            {
                timerPlay.Enabled = false;
                timerPlay.Stop();
                btnPlay.Text = "Play";
            }
            else
            {
                timerPlay.Enabled = true;
                timerPlay.Start();
                btnPlay.Text = "Stop";
            }
        }

        private void timerPlay_Tick(object sender, EventArgs e)
        {
            next();
        }

        private void trackBar_ValueChanged(object sender, EventArgs e)
        {
            if (!downloaded[trackBar.Value] && !downloading)
            {
                Stream stream = client.OpenRead(String.Format(template, pos[trackBar.Value].lat, pos[trackBar.Value].lon, pos[trackBar.Value].angle));
                img[trackBar.Value] = new Bitmap(stream);
                stream = client.OpenRead(String.Format(templateMap, pos[trackBar.Value].lat, pos[trackBar.Value].lon));
                imgMap[trackBar.Value] = new Bitmap(stream);
                downloaded[trackBar.Value] = true;
                //img[trackBar.Value].Save("Images/" + trackBar.Value + ".jpg", ImageFormat.Jpeg);
            }
            pictureBox.Image = img[trackBar.Value];
            pictureBoxMap.Image = imgMap[trackBar.Value];
            elevationChart.Series["Current"].Points.Clear();
            elevationChart.Series["Current"].Points.AddXY(trackBar.Value, pos[trackBar.Value].at);
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            next();
        }

        private void next()
        {
            if (trackBar.Value == trackBar.Maximum) return;
            trackBar.Value++;
        }

        private void trackBarSpeed_ValueChanged(object sender, EventArgs e)
        {
            timerPlay.Interval = 1000 / trackBarSpeed.Value;
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            if (downloading) return;
            downloading = true;
            string path;
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    path = fbd.SelectedPath;
                }
                else return;
            }
            int index = 0;
            foreach (var p in pos)
            {
                downloadImg(p.lat, p.lon, p.angle, path + "/" + index + ".jpg", index);
                progressBar.Value = index * 100 / pos.Count;
                next();
                index++;
            }
            downloading = false;
        }

        private Bitmap downloadImg(double lat, double lon, double angle, string path, int index)
        {
            Stream stream = client.OpenRead(String.Format(template, lat, lon, angle));
            downloaded[index] = true;
            Bitmap btm1 = new Bitmap(stream);
            stream = client.OpenRead(String.Format(templateMap, lat, lon));
            Bitmap btm2 = new Bitmap(stream);
            Bitmap tempBitmap = new Bitmap(btm2.Width, btm2.Height);
            using (Graphics g = Graphics.FromImage(tempBitmap))
            {
                // Draw the original bitmap onto the graphics of the new bitmap
                g.DrawImage(btm2, 0, 0);
                g.DrawString(pos[index].distance.ToString("#.00") + "km", new Font("Arial", 14), Brushes.Black, 100, 100);
                g.Flush();
            }
            Bitmap concat = createPic(btm1, btm2);
            concat.Save(path, ImageFormat.Jpeg);
            downloaded[index] = true;
            img[index] = btm1;
            imgMap[index] = btm2;
            return concat;
        }

        private Bitmap createPic(Bitmap street, Bitmap map)
        {
            int width = street.Width + map.Width;
            int height = Math.Max(street.Height, map.Height);
            Bitmap fullBmp = new Bitmap(width, height);
            Graphics gr = Graphics.FromImage(fullBmp);
            gr.DrawImage(street, 0, 0, street.Width, street.Height);
            gr.DrawImage(map, street.Width, 0);
            return fullBmp;
        }

        private void createMovie(string path)
        {
            int width = pictureBox.Width + pictureBoxMap.Width;
            int height = pictureBox.Height;
            var framRate = 10;
            DirectoryInfo d = new DirectoryInfo(path);
            using (var vFWriter = new VideoFileWriter())
            {
                vFWriter.Open(path + ".avi", width, height, framRate, Accord.Video.FFMPEG.VideoCodec.Default, 10000000);
                int size = d.GetFiles("*.jpg").Length;
                for (int i = 0; i < size; i++)
                {
                    using (Stream BitmapStream = File.Open(path + "/" + i + ".jpg", FileMode.Open))
                    {
                        Image img = Image.FromStream(BitmapStream);

                        Bitmap btm = new Bitmap(img);
                        vFWriter.WriteVideoFrame(btm);
                    }
                }
                vFWriter.Close();
            }
        }

        private void btnMovie_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    createMovie(fbd.SelectedPath);
                }
            }
        }

        private void pictureBoxMap_Paint(object sender, PaintEventArgs e)
        {
            using (Font myFont = new Font("Arial", 14))
            {
                e.Graphics.DrawString(pos[trackBar.Value].distance.ToString(), myFont, Brushes.Green, new System.Drawing.Point(2, 2));
            }
        }
    }
}
