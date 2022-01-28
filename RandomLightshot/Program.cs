using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using HtmlAgilityPack;

namespace RandomLightshot
{
    static class Program
    {
        static List<ImageHistory> imageHistory = new List<ImageHistory>();
        static int historyLocation = 0;
        static int maxHistory = 500;

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

            Form_KeyDown(null, new KeyEventArgs(Keys.Space));

            Application.Run(form);
        }

        private static void Image_SaveAs(object sender, EventArgs e)
        {
            System.Drawing.Image image = form.image.Image;
            string extension = Path.GetExtension(imageHistory[historyLocation].imageLink);

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = extension + " Image|*" + extension;
            saveDialog.Title = "Save Image As";
            saveDialog.ShowDialog();

            if (saveDialog.FileName != "")
            {
                FileStream fs = (FileStream)saveDialog.OpenFile();
                image.Save(fs, System.Drawing.Imaging.ImageFormat.Jpeg);
                fs.Close();
            }
        }

        private static void Image_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
                form.contextMenuStrip.Show(form, e.Location);
        }

        private static void Form_KeyDown(object sender, KeyEventArgs e)
        {
            random = new Random(random.Next(0, int.MaxValue / random.Next(1, 3)));

            if(e.KeyCode == Keys.Space || e.KeyCode == Keys.Right)
            {
                if (historyLocation >= imageHistory.Count-1)
                {
                    string link = "https://prnt.sc/" + RandomString(6);

                    HtmlWeb web = new HtmlWeb();
                    HtmlAgilityPack.HtmlDocument doc = web.Load(link);

                    List<HtmlNode> imageNodes = null;
                    imageNodes = (from HtmlNode node in doc.DocumentNode.SelectNodes("//img")
                                      //where node.Name == "img"
                                  where node.Attributes["class"] != null
                                  //&& node.Attributes["class"].Value.StartsWith("img_")
                                  select node).ToList();

                    bool foundImage = false;

                    foreach (HtmlNode node in imageNodes)
                    {
                        foreach (var attrib in node.GetAttributes())
                        {
                            if (attrib.Value.Contains("imgur") || attrib.Value.Contains("image.prntscr"))
                            {
                                form.image.Image = DownloadImageFromUrl(attrib.Value);
                                imageHistory.Add(new ImageHistory(link, attrib.Value, form.image.Image));
                                historyLocation = imageHistory.Count-1;

                                foundImage = true;
                            }

                            Console.WriteLine(attrib.Value);
                        }
                    }

                    if (!foundImage)
                    {
                        Form_KeyDown(sender, e);
                        return;
                    }

                    while(imageHistory.Count >= maxHistory)
                    {
                        imageHistory.RemoveAt(0);

                        if (historyLocation > imageHistory.Count - 1)
                            historyLocation = imageHistory.Count - 1;
                    }

                    SetFormTitle(imageHistory[historyLocation].link);
                }
                else
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

        static System.Drawing.Image DownloadImageFromUrl(string imageUrl)
        {
            System.Drawing.Image image;

            try
            {
                System.Net.HttpWebRequest webRequest = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(imageUrl);
                webRequest.AllowWriteStreamBuffering = true;
                webRequest.Timeout = 30000;

                System.Net.WebResponse webResponse = webRequest.GetResponse();

                System.IO.Stream stream = webResponse.GetResponseStream();

                image = System.Drawing.Image.FromStream(stream);

                webResponse.Close();
            }
            catch
            {
                return null;
            }

            return image;
        }

        private static Random random = new Random();

        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        struct ImageHistory
        {
            public string link;
            public string imageLink;
            public System.Drawing.Image image;

            public ImageHistory(string url, string imageUrl, System.Drawing.Image img)
            {
                link = url;
                image = img;
                imageLink = imageUrl;
            }
        }
    }
}
