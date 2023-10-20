using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Configuration;

namespace tjk_video_resizer
{
    public partial class Form1 : Form
    {
        private OpenFileDialog videoFile = new OpenFileDialog();

        private string videoFilePath = "";
        private string videoFileName = "";

        private int defaultWidth = 1280;
        private int defaultHeight = 720;

        private string newOutputVideoFilePath;
        private string outputVideoFilePath;

        private List<Process> runningProcesses = new List<Process>();

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // Genişlik ve yükseklik kutucuklarını kilitle ve varsayılan boyutu 1280x720 olarak ayarla
            textBox1.Enabled = false;
            textBox2.Enabled = false;

            textBox1.Text = defaultWidth.ToString();
            textBox2.Text = defaultHeight.ToString();

            // Varsayılan video başlığını, yeni video başlığı listesinin ilk elemanı olarak ayarla
            comboBox1.SelectedIndex = 0;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (MessageBox.Show("Çıkmak istediğinizden emin misiniz?",
                       "Çıkış Ekranı",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Information) == DialogResult.No)
            {
                e.Cancel = true;
            }
            else
            {
                foreach (Process process in runningProcesses)
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                        }
                        process.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex.Message);
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SelectVideo();
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            // Video dönüştürme işlemi sırasında form'u etkisizleştir ve işlem bittiğinde tekrar aktif et
            DisableForm();
            await ResizeVideo();
            EnableForm();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            // Varsayılan boyutlar kutucuğu işaretlendiğinde genişlik ve yükseklik kutucuklarını kilitle ve varsayılan boyutu 1280x720 olarak ayarla
            if (checkBox1.Checked == true)
            {
                textBox1.Enabled = false;
                textBox2.Enabled = false;

                textBox1.Text = defaultWidth.ToString();
                textBox2.Text = defaultHeight.ToString();
            }
            // Varsayılan boyutlar kutucuğu işaretlenmediğinde genişlik ve yükseklik kutucuklarını tekrar aktif et
            else
            {
                textBox1.Enabled = true;
                textBox2.Enabled = true;
            }
        }

        private void SelectVideo()
        {
            videoFile.RestoreDirectory = true;
            videoFile.Title = "Lütfen bir video seçin...";

            // Kullanıcının video seçmesi için dosya seçeceği bir ekran aç
            if (videoFile.ShowDialog() == DialogResult.OK)
            {
                videoFilePath = videoFile.FileName;
                videoFileName = videoFile.SafeFileName;

                label2.Text = "Video Yolu: " + videoFilePath;
                label3.Text = "Video Başlığı: " + videoFileName;

                // Varsayılan boyutlar kutucuğu işaretlenmediğinde genişlik ve yükseklik kutucuklarının boyutunu seçilen videonun boyutu olarak ayarla
                if (checkBox1.Checked == false)
                {
                    string ffprobePath = ExtractFFprobe();

                    textBox1.Text = "";
                    textBox2.Text = "";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = ffprobePath,
                        Arguments = $"-v error -select_streams v:0 -show_entries stream=width,height -of csv=s=x:p=0 {videoFilePath}",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process process = new Process { StartInfo = startInfo })
                    {
                        process.Start();
                        runningProcesses.Add(process);
                        string output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        string[] lines = output.Split('\n');
                        if (lines.Length >= 2)
                        {
                            string[] dimensions = lines[0].Split('x');
                            if (dimensions.Length == 2)
                            {
                                int width = int.Parse(dimensions[0]);
                                int height = int.Parse(dimensions[1]);

                                textBox1.Text = width.ToString();
                                textBox2.Text = height.ToString();
                            }
                        }
                    }
                }
            }
        }

        private async Task ResizeVideo()
        {
            FolderBrowserDialog outputFolder = new FolderBrowserDialog();

            // Video seçilmediyse işlem iptal edilir
            if (!string.IsNullOrWhiteSpace(videoFilePath))
            {
                // Kullanıcının dönüştürülecek videoya yeni bir konum seçmesi için klasör seçme ekranı aç
                if (outputFolder.ShowDialog() == DialogResult.OK)
                {
                    outputVideoFilePath = Path.Combine(outputFolder.SelectedPath, videoFileName);
                    string selectedOutputName = Path.GetFileNameWithoutExtension(comboBox1.SelectedItem.ToString());

                    // Set the newOutputVideoFilePath with the new name before starting the process
                    newOutputVideoFilePath = Path.Combine(outputFolder.SelectedPath, selectedOutputName + Path.GetExtension(videoFilePath));

                    // Mevcut dosya kontrolü yap. Eğer aynı isimde bir video mevcutsa üzerine yazmak için bir ekran aç
                    if (File.Exists(newOutputVideoFilePath))
                    {
                        var result = MessageBox.Show("Aynı isimli bir dosya mevcut. Üzerine yazmak ister misiniz?", "Aynı İsimli Dosya Mevcut", MessageBoxButtons.YesNo);

                        if (result != DialogResult.Yes)
                        {
                            return;
                        }
                        else if (result == DialogResult.Yes)
                        {
                            File.Delete(newOutputVideoFilePath);
                        }
                    }

                    // Dönüştürme için gerekli işlemleri yap
                    string ffmpegPath = ExtractFFmpeg();
                    string arguments = $"-loglevel info -i \"{videoFilePath}\" -vf \"scale={textBox1.Text}:{textBox2.Text}\" -c:a copy";

                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = ffmpegPath,
                        Arguments = $"{arguments} \"{newOutputVideoFilePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process process = new Process { StartInfo = startInfo })
                    {
                        process.EnableRaisingEvents = true;

                        process.Start();
                        runningProcesses.Add(process);
                        await WaitForExitAsync(process);
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                MessageBox.Show("Lütfen ilk önce bir video seçin!");
            }
        }

        /* private void PostVideoToFTP()
         {
             FtpWebRequest request = (FtpWebRequest)WebRequest.Create(ConfigurationManager.AppSettings["ftpurl"]);

             request.Method = WebRequestMethods.Ftp.ListDirectoryDetails;
             request.Credentials = new NetworkCredential(ConfigurationManager.AppSettings["username"], ConfigurationManager.AppSettings["password"]);
             request.KeepAlive = false;
             request.UseBinary = true;
             request.UsePassive = true;
         }*/


        // ffmpeg.exe'yi çağır ve string bir değişken olarak döndür
        private string ExtractFFmpeg()
        {
            string ffmpegResourceName = "tjk_video_resizer.ffmpeg.exe";
            string ffmpegFilePath = Path.Combine(Path.GetTempPath(), "ffmpeg.exe");

            if (!File.Exists(ffmpegFilePath))
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream resourceStream = assembly.GetManifestResourceStream(ffmpegResourceName))
                {
                    if (resourceStream != null)
                    {
                        using (FileStream fileStream = new FileStream(ffmpegFilePath, FileMode.Create))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                }
            }

            return ffmpegFilePath;
        }

        // ffprobe.exe'yi çağır ve string bir değişken olarak döndür
        private string ExtractFFprobe()
        {
            string ffprobeResourceName = "tjk_video_resizer.ffprobe.exe";
            string ffprobeFilePath = Path.Combine(Path.GetTempPath(), "ffprobe.exe");

            if (!File.Exists(ffprobeFilePath))
            {
                Assembly assembly = Assembly.GetExecutingAssembly();
                using (Stream resourceStream = assembly.GetManifestResourceStream(ffprobeResourceName))
                {
                    if (resourceStream != null)
                    {
                        using (FileStream fileStream = new FileStream(ffprobeFilePath, FileMode.Create))
                        {
                            resourceStream.CopyTo(fileStream);
                        }
                    }
                }
            }

            return ffprobeFilePath;
        }

        // Dönüştürme işlemi tamamlandığında yapılacak işlemler
        private Task WaitForExitAsync(Process process)
        {
            var tcs = new TaskCompletionSource<bool>();

            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                process.EnableRaisingEvents = false;
                process.Dispose();
                tcs.SetResult(true);

                MessageBox.Show("Dönüştürme İşlemi Tamamlandı!");
            };

            return tcs.Task;
        }

        // Form'u etkisizleştir
        private void DisableForm()
        {
            // Button
            button1.Enabled = false;
            button2.Enabled = false;

            // TextBox
            textBox1.Enabled = false;
            textBox2.Enabled = false;

            // CheckBox
            checkBox1.Enabled = false;

            // ComboBox
            comboBox1.Enabled = false;
        }

        // Form'u aktif et
        private void EnableForm()
        {
            // Button
            button1.Enabled = true;
            button2.Enabled = true;

            // TextBox
            if (checkBox1.Checked == false)
            {
                textBox1.Enabled = true;
                textBox2.Enabled = true;
            }

            // CheckBox
            checkBox1.Enabled = true;

            // ComboBox
            comboBox1.Enabled = true;
        }
    }
}
