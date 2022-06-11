using System.Diagnostics;
using System.Runtime.InteropServices;
using Octokit;
using System.Text;

namespace Sim70
{
    public partial class MainForm : Form
    {
        private Point joinButtonCoords;
        private Point serverCoords;
        private bool isRunning;
        private readonly int ver = 4;
        private Color joiningColor;
        private Log log;
        private DateTime started;
        private int joinCounter = 0;
        private int secondsPassed = 0;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

        [DllImport("user32.dll")]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public struct Rect
        {
            public int Left { get; set; }
            public int Top { get; set; }
            public int Right { get; set; }
            public int Bottom { get; set; }
        }

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr hwnd, ref Rect rectangle);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        public MainForm()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        {
            InitializeComponent();
            RegisterHotKey(Handle, 1, 0, (int)Keys.F2);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            log = new Log(rtbLog);

            for (int i = 1; i <= 10; i++)
            {
                cmbHotkey.Items.Add("F" + i.ToString());
            }
            cmbHotkey.SelectedIndex = 1;

            loadSettings();

            var client = new GitHubClient(new ProductHeaderValue("SIM70"));
            var releases = client.Repository.Release.GetAll("lkd70", "SIM70");
            var latest = releases.Result[0];

            log.Append("The latest Sim70 release on GitHub is version " + latest.Id);

            if (latest.Id < ver)
            {
                log.Append($"New update found. Current version: {ver}. Latest GitHub release version: {latest.Id}");
                string message = $"An update is available for Sim70." +
                 $"\nYou're currently on version {ver}, however version {latest.Id} is available." +
                 $"\nDownload the new verison now?";
                DialogResult result = MessageBox.Show(message, "Update Available", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (result == DialogResult.Yes)
                {
                    OpenBrowser(latest.Assets[0].BrowserDownloadUrl);
                    MessageBox.Show("Download has started in your browser. Please close Sim70 and re-open the new file.");
                }
            }

            Text = $"SIM70 v{ver} - Kibble on top";
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1)
            {
                toggleSimming();
            }
            base.WndProc(ref m);
        }

        IntPtr GetProcess(string name = "Shootergame")
        {
            Process[] processesByName = Process.GetProcessesByName(name);
            if (processesByName.Length != 0)
            {
                log.Append("process lookup: " + processesByName.Length + ", Handle: " + processesByName[0].MainWindowHandle, Log.Type.Work);
                return processesByName[0].MainWindowHandle;
            } else
            {
                return IntPtr.Zero;
            }
        }

        private void SetText(string text, Control control)
        {
            if (control.InvokeRequired)
                control.Invoke(new Action(() => control.Text = text));
            else
                control.Text = text;
        }

        private void updateStats()
        {
            SetText("First Started Simming: " + started.ToString("yyyy-MM-dd HH:mm:ss"), lblFirstStartedSimming);
            SetText("Total Simming Duration: " + TimeSpan.FromSeconds(secondsPassed).ToString(@"hh\:mm\:ss"), lblTotalSimmingDuration);
            SetText("Total join clicks: " + joinCounter.ToString("#,##0"), lblJoinClicks);
        }
        private void toggleStatus(bool running)
        {
            lblStatus.Text = running ? "Running" : "Stopped";
            lblStatus.ForeColor = running ? Color.Green : Color.Red;
        }
        private void toggleSimming()
        {
            isRunning = !isRunning;
            toggleStatus(isRunning);
            if (joinButtonCoords.IsEmpty)
            {
                toggleStatus(false);
                MessageBox.Show("Please drag the box in the UI to the Join button on the ARK Screen.");
            } else
            {
                if (isRunning)
                {
                    started = DateTime.Now;
                    btnStatus.Text = "Stop (F2)";
                    new Task(new Action(loopClicker)).Start();
                }
                else
                {
                    btnStatus.Text = "Start (F2)";
                }
            }

        }

        void sendDiscord()
        {
            string user = (txtMentionId.Text != "") ? $"<@{txtMentionId.Text}> - " : "";
            string message = @"{
              ""content"": null,
              ""embeds"": [
                {
                  ""title"": ""You're in!"",
                  ""description"": """ + user + @"Rumour has it you've got in to the server. I can't confirm this so please check."",
                  ""fields"": [
                    {
                      ""name"": ""Started Simming"",
                      ""value"": """ + started.ToString("yyyy-MM-dd HH:mm:ss") + @""",
                      ""inline"": true
                    },
                    {
                      ""name"": ""Total Sim Duration"",
                      ""value"": """ + TimeSpan.FromSeconds(secondsPassed).ToString(@"hh\:mm\:ss") + @""",
                      ""inline"": true
                    },
                    {
                      ""name"": ""Join Clicks"",
                      ""value"": """ + joinCounter.ToString("#,##0") + @""",
                      ""inline"": true
                    }
                  ],
                  ""author"": {
                    ""name"": ""Sim70"",
                    ""url"": ""https://github.com/lkd70/SIM70""
                  }
                }
              ]
            }";



            if (txtDiscordWebhook.Text != null)
            {
                HttpClient client = new HttpClient();
                var content = new StringContent(message, Encoding.UTF8, "application/json");
                var result = client.PostAsync(txtDiscordWebhook.Text, content).Result;

                //String mentionID = (txtMentionId.Text != null) ? $"<@{txtMentionId.Text}> - " : "";
                //embed.Description = mentionID + "Rumour has it you've got in to the server. I can't confirm this so please check.";

            }
        }

        int WaitForJoinable(IntPtr process)
        {
            int seconds = 0;
            while (true)
            {
                if (!isRunning) return 0;
                seconds++;
                secondsPassed++;
                updateStats();

                Thread.Sleep(1000);
                if (process != IntPtr.Zero)
                {
                    Point coords = getAdjustedCoords(process, joinButtonCoords);
                    Color current = GetPixelColor(process, coords.X, coords.Y);

                    IEnumerable<int> redRange   = Enumerable.Range(pickerJoin.Color.R - 5, pickerJoin.Color.R + 5);
                    IEnumerable<int> greenRange = Enumerable.Range(pickerJoin.Color.G - 5, pickerJoin.Color.G + 5);
                    IEnumerable<int> blueRange  = Enumerable.Range(pickerJoin.Color.B - 5, pickerJoin.Color.B + 5);

                    if (redRange.Contains(current.R) && greenRange.Contains(current.G) && blueRange.Contains(current.B))
                    {
                        log.Append("Join button detected", Log.Type.Work);
                        return seconds;
                    } else if (seconds >= 30)
                    {
                        return 30;
                    } else
                    {
                        // Join button still pressed.
                        log.Append("Waiting for JOIN button, seconds: " + seconds, Log.Type.Work);
                        if (joiningColor == Color.Empty)
                        {
                            log.Append("Writing joiningColor. Value: " + current.ToString(), Log.Type.Work);
                            joiningColor = current;
                        } else
                        {
                            // Check if we're potentially in the server.
                            redRange = Enumerable.Range(joiningColor.R - 10, joiningColor.R + 10);
                            greenRange = Enumerable.Range(joiningColor.G - 10, joiningColor.G + 10);
                            blueRange = Enumerable.Range(joiningColor.B - 10, joiningColor.B + 10);
                            if (redRange.Contains(current.R) && greenRange.Contains(current.G) && blueRange.Contains(current.B))
                            {
                                // Join button is pressed - Not an unknown colour.
                                log.Append("In check: No", Log.Type.Work);
                            } else
                            {
                                // Join button (clicked or unclicked) was not found, potentially in server?
                                log.Append("In check: Maybe?", Log.Type.Work);
                                if (chkAutoStopSim.Checked)
                                {
                                    if (chkDiscord.Checked) sendDiscord();
                                    log.Append("Assuming we got in to the server. Disabling sim.", Log.Type.Done);
                                    toggleSimming();
                                }
                            }
                        }
                    }
                } else
                {
                    return 0;
                }
            }
        }

        private void loopClicker()
        {
            int loopCount = 0;
            IntPtr hWnd_pc = GetProcess();


            if (hWnd_pc != IntPtr.Zero)
            {
                while (isRunning)
                {

                    loopCount = loopCount + 1;
                    Point mousePosition = MousePosition;
                    SetCursorPos(joinButtonCoords.X, joinButtonCoords.Y);
                    PostMessage(hWnd_pc, 513U, 0, 0);
                    PostMessage(hWnd_pc, 514U, 0, 0);
                    log.Append("Clicked join");
                    joinCounter++;
                    Thread.Sleep(1);


                    SetCursorPos(mousePosition.X, mousePosition.Y);

                    int taken = 0;
                    if (chkAutoDelay.Checked)
                    {
                        taken = WaitForJoinable(hWnd_pc);
                        log.Append("Waited " + taken + " seconds for the join button", Log.Type.Work);
                    }

                    if (chkSelectServer.Checked && (taken >=30) && !serverCoords.IsEmpty)
                    {
                        log.Append("Selecting server from list as 30 seconds have passed since last join button was visible.", Log.Type.Warning);
                        SetCursorPos(serverCoords.X, serverCoords.Y);
                        PostMessage(hWnd_pc, 513U, 0, 0);
                        PostMessage(hWnd_pc, 514U, 0, 0);
                        Thread.Sleep(1);
                        SetCursorPos(mousePosition.X, mousePosition.Y);
                        Thread.Sleep((int)nudDelay.Value);
                        Thread.Sleep((int)nudDelay.Value);

                    }

                    Thread.Sleep((int)nudDelay.Value);
                }
            } else
            {
                log.Append("Please ensure ARK is running, couldn't locate the process", Log.Type.Error);
            }
        }

        private void screenColorPicker1_MouseUp(object sender, MouseEventArgs e)
        {
            joinButtonCoords = Cursor.Position;
        }

        private void btnStatus_Click(object sender, EventArgs e)
        {
            toggleSimming();
        }

        public static void OpenBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                url = url.Replace("&", "^&");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
            }
        }

        private void scpServerSelect_MouseUp(object sender, MouseEventArgs e)
        {
            serverCoords = Cursor.Position;
        }

        private void chkSelectServer_CheckStateChanged(object sender, EventArgs e)
        {
            scpServerSelect.Visible = chkSelectServer.Checked;
        }

        private void btnDonate_Click(object sender, EventArgs e)
        {
            OpenBrowser("https://github.com/sponsors/lkd70");
        }

        public static Color GetPixelColor(IntPtr hwd, int x, int y)
        {
            IntPtr dc = GetDC(hwd);
            uint pixel = GetPixel(dc, x, y);
            ReleaseDC(hwd, dc);
            return Color.FromArgb((int)pixel & (int)byte.MaxValue, ((int)pixel & 65280) >> 8, ((int)pixel & 16711680) >> 16);
        }

        private Point getAdjustedCoords(IntPtr hWnd_pc, Point coords)
        {
            Rect window = new Rect();
            GetWindowRect(hWnd_pc, ref window);
            Point joinCoords = new Point();
            joinCoords.X = coords.X - window.Left;
            joinCoords.Y = coords.Y;
            return joinCoords;
        }

        Keys resolveHotkey()
        {
            if (cmbHotkey.SelectedItem.ToString() != null)
            {
                switch(cmbHotkey.SelectedItem.ToString())
                {
                    case "F1": return Keys.F1;
                    case "F2": return Keys.F2;
                    case "F3": return Keys.F3;
                    case "F4": return Keys.F4;
                    case "F5": return Keys.F5;
                    case "F6": return Keys.F6;
                    case "F7": return Keys.F7;
                    case "F8": return Keys.F8;
                    case "F9": return Keys.F9;
                    case "F10": return Keys.F10;
                    default: return Keys.F2;
                }
            }
            return Keys.F2;
        }

        private void cmbHotkey_SelectedIndexChanged(object? sender, EventArgs? e)
        {
            Keys hotkey = resolveHotkey();
            btnStatus.Text = (isRunning ? "Stop" : "Start") + " (" + hotkey.ToString() + ")";
            UnregisterHotKey(Handle, 1);
            RegisterHotKey(Handle, 1, 0, (int)hotkey);
            Properties.Settings.Default.Hotkey = hotkey.ToString();
            Properties.Settings.Default.Save();
        }

        private void chkDiscord_CheckedChanged(object? sender, EventArgs? e)
        {
            txtDiscordWebhook.Enabled = chkDiscord.Checked;
            txtMentionId.Enabled = chkDiscord.Checked;
            Properties.Settings.Default.DiscordAlerts = chkDiscord.Checked;
            Properties.Settings.Default.Save();
        }

        private void loadSettings()
        {
            chkAutoDelay.Checked = Properties.Settings.Default.AutoClickrateMode;
            nudDelay.Value = Properties.Settings.Default.ClickRate;
            chkAutoStopSim.Checked = Properties.Settings.Default.AutoStopSim;

            chkDiscord.Checked = Properties.Settings.Default.DiscordAlerts;
            chkDiscord_CheckedChanged(null, null);

            txtDiscordWebhook.Text = Properties.Settings.Default.DiscordWebhook;
            txtMentionId.Text = Properties.Settings.Default.DiscordMention;

            cmbHotkey.SelectedItem = Properties.Settings.Default.Hotkey;
            cmbHotkey_SelectedIndexChanged(null, null);



        }
        private void chkAutoStopSim_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.AutoStopSim = chkAutoStopSim.Checked;
            Properties.Settings.Default.Save();
        }

        private void txtDiscordWebhook_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DiscordWebhook = txtDiscordWebhook.Text;
            Properties.Settings.Default.Save();
        }

        private void txtMentionId_TextChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.DiscordMention = txtMentionId.Text;
            Properties.Settings.Default.Save();
        }

        private void nudDelay_ValueChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.ClickRate = (int)nudDelay.Value;
            Properties.Settings.Default.Save();
        }

        private void chkAutoDelay_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.AutoClickrateMode = chkAutoDelay.Checked;
            Properties.Settings.Default.Save();
        }
    }
}