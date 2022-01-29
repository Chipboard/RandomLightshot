using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

using HtmlAgilityPack;

namespace RandomLightshot
{
    static class Program
    {
        static List<ImageHistory> imageHistory = new List<ImageHistory>();
        static int historyLocation = 0;
        static int maxHistory = 501;

        static bool formClosed;

        static Form form;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            form = new Form();

            form.KeyDown += Form_KeyDown;
            form.image.MouseClick += Image_MouseClick;

            form.saveAsToolStripMenuItem.Click += Image_SaveAs;
            form.copyToolStripMenuItem.Click += Image_Copy;
            form.openLinkToolStripMenuItem.Click += Image_OpenLink;

            form.FormClosed += Form_Closed;

            for (int i = 0; i < 4; i++)
            {
                Thread updateThread = new Thread(Update);
                updateThread.Name = "Update Thread " + i;
                updateThread.Start();

                Console.Write("Begginning search thread " + i);
            }

            Application.Run(form);
        }

        private static void Image_OpenLink(object sender, EventArgs e)
        {
            OpenUrl(imageHistory[historyLocation].link);
        }

        static void OpenUrl(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    throw;
                }
            }
        }

        private static void Image_Copy(object sender, EventArgs e)
        {
            if(form.image.Image != null)
                Clipboard.SetImage(form.image.Image);
        }

        private static void Form_Closed(object sender, FormClosedEventArgs e)
        {
            formClosed = true;
        }

        static void Update()
        {
            while (!formClosed)
            {
                Thread.Sleep(random.Next(10, 100));

                if (imageHistory.Count + 1 >= maxHistory && historyLocation >= maxHistory / 2)
                {
                    Console.WriteLine("getIsMax");
                    GetImage();
                }
                else if (imageHistory.Count + 1 < maxHistory)
                {
                    Console.WriteLine("getIsBuffer: " + (imageHistory.Count + 1) + " | " + maxHistory);
                    GetImage();
                }
            }
        }

        private static void Image_SaveAs(object sender, EventArgs e)
        {
            if (form.image.Image == null)
                return;

            Image image = form.image.Image;
            string extension = new ImageFormatConverter().ConvertToString(image.RawFormat).ToLower();

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = extension + " Image|*" + extension;
            saveDialog.FileName = image.HorizontalResolution + " x " + image.VerticalResolution;
            saveDialog.Title = "Save Image As";
            saveDialog.ShowDialog();

            if (saveDialog.FileName != "")
            {
                saveDialog.FileName = saveDialog.FileName + "." + extension;
                FileStream fs = (FileStream)saveDialog.OpenFile();
                image.Save(fs, ImageFormat.Jpeg);
                fs.Close();
            }
        }

        private static void Image_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right && form.image.Image != null)
                form.contextMenuStrip.Show(form, e.Location);
        }

        static bool GetImage()
        {
            random = new Random(random.Next(0, int.MaxValue / random.Next(1, 3)));

            bool foundImage = false;
            string link = "";

            switch (random.Next(0,2))
            {
                //Lightshot
                case 0:
                    link = "https://prnt.sc/" + RandomString(6, lighsthotCharSet);

                    HtmlWeb web = new HtmlWeb();
                    HtmlAgilityPack.HtmlDocument doc = web.Load(link);

                    List<HtmlNode> imageNodes = null;
                    imageNodes = (from HtmlNode node in doc.DocumentNode.SelectNodes("//img")
                                      //where node.Name == "img"
                                  where node.Attributes["class"] != null
                                  //&& node.Attributes["class"].Value.StartsWith("img_")
                                  select node).ToList();

                    foreach (HtmlNode node in imageNodes)
                    {
                        foreach (var attrib in node.GetAttributes())
                        {
                            if (attrib.Value.Contains("imgur") || attrib.Value.Contains("image.prntscr"))
                            {
                                imageHistory.Add(new ImageHistory(link, attrib.Value, DownloadImageFromUrl(attrib.Value)));
                                foundImage = true;
                            }

                            Console.WriteLine(attrib.Value);
                        }
                    }

                    break;

                    //Imgur
                case 1:
                    link = "http://i.imgur.com/" + RandomString(random.Next(4,7), imgurCharSet) + ".jpg";

                    Image imgurImage = DownloadImageFromUrl(link);

                    int tries = 0;
                    while (imgurImage == null)
                    {
                        tries++;

                        link = "http://i.imgur.com/" + RandomString(random.Next(4, 7), imgurCharSet) + ".jpg";
                        imgurImage = DownloadImageFromUrl(link);

                        if (tries > 10)
                            break;
                    }
                    Console.WriteLine(link);
                    imageHistory.Add(new ImageHistory(link, link, DownloadImageFromUrl(link)));
                    foundImage = true;

                    break;
            }

            while (imageHistory.Count >= maxHistory)
            {
                imageHistory.RemoveAt(0);
                historyLocation--;
            }

            return foundImage;
        }

        private static void Form_KeyDown(object sender, KeyEventArgs e)
        {
            if(e.KeyCode == Keys.Space || e.KeyCode == Keys.Right)
            {
                if (historyLocation < imageHistory.Count-1)
                {
                    historyLocation++;

                    if (historyLocation >= imageHistory.Count)
                    {
                        historyLocation = imageHistory.Count - 1;
                        Form_KeyDown(sender, e);
                        return;
                    }

                    form.image.Image = imageHistory[historyLocation].image;
                    SetFormTitle(imageHistory[historyLocation].link);
                } else
                {
                    historyLocation = imageHistory.Count - 1;
                }
            }

            if(e.KeyCode == Keys.Left || e.KeyCode == Keys.ControlKey)
            {
                if(historyLocation > 0 && imageHistory.Count > 0)
                {
                    historyLocation--;

                    form.image.Image = imageHistory[historyLocation].image;
                    SetFormTitle(imageHistory[historyLocation].link);
                }
            }
        }

        static void SetFormTitle(string title)
        {
            string newTitle = title + " (" + (historyLocation + 1) + "/" + imageHistory.Count + ")";
            form.Text = newTitle;
        }

        static Image DownloadImageFromUrl(string imageUrl)
        {
            Image image;

            try
            {
                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(imageUrl);
                webRequest.AllowWriteStreamBuffering = true;
                webRequest.Timeout = 30000;

                System.Net.WebResponse webResponse = webRequest.GetResponse();

                Stream stream = webResponse.GetResponseStream();

                image = Image.FromStream(stream);

                if (image.Width == 161)
                    image = null;

                webResponse.Close();
            }
            catch
            {
                return null;
            }

            return image;
        }

        private static Random random = new Random();
        const string lighsthotCharSet = "abcdefghijklmnopqrstuvwxyz0123456789";
        const string imgurCharSet = "AaBbCcDdEeFfGgHhIiJjKkLlMmNnOoPpQqRrSsTtUuVvWwXxYyZz0123456789";

        public static string RandomString(int length, string chars)
        {
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        struct ImageHistory
        {
            public string link;
            public string imageLink;
            public Image image;

            public ImageHistory(string url, string imageUrl, Image img)
            {
                link = url;
                image = img;
                imageLink = imageUrl;
            }
        }
    }
}
