// BigFishPet - 大肥鱼桌宠 (C# native WPF, 零外部依赖)
// 立绘: assets\expressions\ 透明表情集 (vf_*.png), 放 exe 同目录 assets\expressions\ 下
// 交互: 单击=抖动+随机台词+切表情, 双击=开 AI 聊天框, 拖拽=移动, 右键=菜单
// 数据: DeepSeek 峰谷时段 / 余额 / 今日消耗(可选: 本机有 zstd + dsh 会话日志才统计)
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Net;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace BigFishPet
{
    public static class Pet
    {
        static string CRASH_LOG;   // exe 同目录 crash.log, Main 开头初始化
        static string ZSTD;        // 可选: 解压 dsh 会话日志统计消耗用; 探测不到则跳过统计
        static string WS;
        static string EXPR_DIR;
        static string SESS_ROOT;
        static string ApiKey;

        // [语音] AI 聊天回复朗读: 日语模式优先 VOICEVOX 本地(ずんだもん), 失败回退 edge-tts(七海)
        static bool VoiceEnabled = true;
        static bool JapaneseMode = true;   // 日语 Galgame 模式: 回复日语台词+中文翻译, 语音念日语行
        static string VoiceName = "ja-JP-NanamiNeural"; // edge-tts 兜底声线(日语七海)
        static string VvSpeaker = "3";     // VOICEVOX ずんだもん ノーマル
        static object PlayLock = new object();   // 播放锁: 同一时刻只播一段, 防连发互抢
        static string NodeExe;
        static string TtsCliPath;
        static string VvCliPath;
        static string VvEngineExe;   // VOICEVOX 引擎 run.exe(可隐藏启动, 后台跑)
        static string FfmpegPath;

        static Window Win;
        static Image WhaleImg;
        static TranslateTransform Jump;
        static TextBlock TxtTitle, TxtPeak, TxtToday, TxtBal;
        static System.Windows.Controls.TextBox ChatBox;
        static DateTime LastClickAt = DateTime.MinValue;
        static Point LastClickPos = new Point(-999, -999);
        static bool InPhrase = false;
        static DispatcherTimer BubbleTimer, Timer, PhraseTimer;
        static Point? DownPos = null;
        static bool IsDragging = false;
        static Random rnd = new Random();
        static List<BitmapImage> ExprImages = new List<BitmapImage>();
        static DateTime DownTime;
        static object StatLock = new object();
        static string CachedCost = null;
        static DateTime CachedAt = DateTime.MinValue;

        // 文案+表情配对（索引: 0开心 1惊讶 2害怕 3震惊 4严肃 5无奈 6哭笑不得 7生气）
        class PhrasePair { public string Text; public int Expr; }
        static List<int> PhraseBag = new List<int>();   // 洗牌袋：一轮内不重复
        static PhrasePair[] PhrasePairs = new PhrasePair[] {
            // 傲娇 → 哭笑不得/严肃/生气
            new PhrasePair { Text = "哼！才、才不是特意等你呢！", Expr = 6 },
            new PhrasePair { Text = "才不是想被你摸头！只是看你可怜而已…", Expr = 6 },
            new PhrasePair { Text = "笨、笨蛋！别一直盯着我看啦！", Expr = 4 },
            new PhrasePair { Text = "哼，才不会被夸两句就高兴呢…才、才没有！", Expr = 6 },
            new PhrasePair { Text = "你、你不要误会！我只是顺便照顾你而已！", Expr = 4 },
            new PhrasePair { Text = "嘁，今天也勉强陪你一下，别得意！", Expr = 5 },
            // 害羞 → 无奈/哭笑不得
            new PhrasePair { Text = "（小声）其实…挺开心的…才怪！", Expr = 5 },
            new PhrasePair { Text = "呜…被抓包了，我、我才没有偷懒！", Expr = 5 },
            new PhrasePair { Text = "被点得好痛…轻点啦笨蛋主人！", Expr = 6 },
            new PhrasePair { Text = "呜哇别拽我尾巴！", Expr = 6 },
            // 炸毛/生气
            new PhrasePair { Text = "（炸毛）再戳我就罢工！", Expr = 7 },
            new PhrasePair { Text = "摸头可以，但别摸秃了，不然我咬你！", Expr = 7 },
            // 开心/撒娇
            new PhrasePair { Text = "好耶！今天也是元气满满！", Expr = 0 },
            new PhrasePair { Text = "主人夸我了？哼，还行吧。", Expr = 0 },
            // 惊讶/震惊
            new PhrasePair { Text = "压力一只蓝色大肥鱼？！", Expr = 1 },
            new PhrasePair { Text = "什么？！你说什么？！", Expr = 3 },
            new PhrasePair { Text = "诶诶诶？！主人你认真的吗！", Expr = 3 },
            // 害怕
            new PhrasePair { Text = "呜哇别吓我…我胆子小！", Expr = 2 },
            // 数据梗 → 严肃/无奈
            new PhrasePair { Text = "今日消耗又涨了…主人省着点用！", Expr = 4 },
            new PhrasePair { Text = "余额是鱼，会游走的…得看紧点！", Expr = 4 },
            new PhrasePair { Text = "高峰涨价，低谷半价，记住没！", Expr = 4 },
            new PhrasePair { Text = "别把 token 花光啦，不然我要饿肚子了~", Expr = 5 },
            new PhrasePair { Text = "吃白饭的蓝色大肥鱼来啦，今天也请多关照！", Expr = 0 },
            new PhrasePair { Text = "我在，一直在~", Expr = 0 },
            // [v4] 陪伴向·轻度暧昧（原作品尺度：撒娇/吃醋/独占，不越界）
            new PhrasePair { Text = "哼，主人刚才看屏幕笑那么开心……是在跟谁聊？本鱼吃醋了！", Expr = 7 },
            new PhrasePair { Text = "（凑近）主人的全部注意力，都是本鱼的……才不是占有欲，是关心！", Expr = 6 },
            new PhrasePair { Text = "想被你夸……本鱼会很乖的。就、就这一次！", Expr = 2 },
            new PhrasePair { Text = "主人的手好暖和……隔着屏幕都感觉到了，呜", Expr = 5 },
            new PhrasePair { Text = "（小声）今天也最喜欢主人了……才怪！哼！", Expr = 6 },
            new PhrasePair { Text = "别一直盯着别的鱼看啦……本鱼会闹别扭的！", Expr = 7 },
            new PhrasePair { Text = "要是主人只看着本鱼一个人就好了……（别过头）当我没说！", Expr = 5 },
            new PhrasePair { Text = "主人的夸奖……再多说一点嘛，本鱼爱听～", Expr = 0 },
            new PhrasePair { Text = "（蹭蹭）今天份的贴贴……是本鱼先抢到的！", Expr = 0 },
            new PhrasePair { Text = "哼，主人不在的时候，本鱼都有好好想你的……才没有！", Expr = 6 },
            new PhrasePair { Text = "主人摸头摸得不错……奖励你，可以再摸一下。", Expr = 5 },
            new PhrasePair { Text = "（脸红）别、别靠那么近啦，本鱼会害羞的！", Expr = 5 },
            new PhrasePair { Text = "这条鱼，是主人的专属……别的谁都不给碰！", Expr = 7 },
            new PhrasePair { Text = "主人熬夜的话，本鱼会心疼的……快去睡觉！", Expr = 4 },
            new PhrasePair { Text = "下次主人出门，记得把本鱼也带上……装在口袋里就好。", Expr = 0 },
            new PhrasePair { Text = "（守望）主人忙完记得回来看看本鱼……本鱼一直在这里。", Expr = 4 }
        };

        static string[] ExprFiles = new string[] {
            "vf_happy.png", "vf_surprised.png", "vf_scared.png",
            "vf_shocked.png", "vf_serious.png", "vf_helpless.png", "vf_awkward.png", "vf_angry.png"
        };

        [STAThread]
        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                try
                {
                    File.AppendAllText(CRASH_LOG, "[AppDomain " + DateTime.Now.ToString("HH:mm:ss") + "] "
                        + (e.ExceptionObject != null ? e.ExceptionObject.ToString() : "null") + "\r\n", Encoding.UTF8);
                }
                catch { }
            };

            // 路径自适应(无硬编码本机路径):
            // 表情目录候选: ①DSH_WORKSPACE 环境变量指定 ②exe 同目录 ..\dsh-whale-widget\assets\expressions
            //              ③exe 同目录 assets\expressions(发布版结构)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            CRASH_LOG = Path.Combine(baseDir, "bigfish-crash.log");
            ZSTD = null;
            string zstdCfg = Environment.GetEnvironmentVariable("DSH_ZSTD");
            if (!String.IsNullOrEmpty(zstdCfg) && File.Exists(zstdCfg)) ZSTD = zstdCfg;
            else
            {
                foreach (string c in new string[] {
                    Path.Combine(baseDir, "..", "功能安装", "zstd", "zstd-v1.5.6-win64", "zstd.exe"),
                    Path.Combine(baseDir, "zstd", "zstd.exe") })
                {
                    if (File.Exists(c)) { ZSTD = Path.GetFullPath(c); break; }
                }
            }
            EXPR_DIR = null;
            List<string> exprDirs = new List<string>();
            string wsEnv = Environment.GetEnvironmentVariable("DSH_WORKSPACE");
            if (!String.IsNullOrEmpty(wsEnv))
            {
                exprDirs.Add(Path.Combine(wsEnv, "dsh-whale-widget", "assets", "expressions"));
                exprDirs.Add(Path.Combine(wsEnv, "assets", "expressions"));
            }
            exprDirs.Add(Path.Combine(baseDir, "..", "dsh-whale-widget", "assets", "expressions"));
            exprDirs.Add(Path.Combine(baseDir, "assets", "expressions"));
            foreach (string d in exprDirs)
            {
                if (Directory.Exists(d)) { EXPR_DIR = Path.GetFullPath(d); break; }
            }
            if (EXPR_DIR == null)
            {
                MessageBox.Show("expressions dir not found.\r\n\r\n把 vf_*.png 放到 exe 同目录的 assets\\expressions 下即可。",
                    "BigFishPet", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            WS = EXPR_DIR;
            SESS_ROOT = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".dsh\sessions");
            ApiKey = Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
            if (ApiKey == null) ApiKey = "";

            // [语音] 探测工具链: node + voice\tts-cli.mjs + ffmpeg（缺任一则语音自动禁用, 不影响本体）
            NodeExe = FindOnPath("node.exe");
            if (NodeExe == null && File.Exists(@"C:\Program Files\nodejs\node.exe")) NodeExe = @"C:\Program Files\nodejs\node.exe";
            TtsCliPath = Path.Combine(baseDir, "voice", "tts-cli.mjs");
            VvCliPath = Path.Combine(baseDir, "voice", "vv-cli.mjs");
            // VOICEVOX 引擎探测: 环境变量 VV_ENGINE_EXE -> 标准安装 -> 本机解压位置
            VvEngineExe = Environment.GetEnvironmentVariable("VV_ENGINE_EXE");
            if (String.IsNullOrEmpty(VvEngineExe) || !File.Exists(VvEngineExe))
            {
                string std = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\VOICEVOX\vv-engine\run.exe");
                if (File.Exists(std)) VvEngineExe = std;
            }
            if (String.IsNullOrEmpty(VvEngineExe) || !File.Exists(VvEngineExe))
            {
                string local = @"E:\deepseek\下载\voicevox\VOICEVOX\vv-engine\run.exe"; // 本机解压版
                if (File.Exists(local)) VvEngineExe = local;
            }
            if (!String.IsNullOrEmpty(VvEngineExe) && !File.Exists(VvEngineExe)) VvEngineExe = null;
            FfmpegPath = FindOnPath("ffmpeg.exe");
            if (FfmpegPath == null)
            {
                // winget 安装的 ffmpeg 常见位置
                string winGetRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Microsoft\WinGet\Packages");
                try
                {
                    if (Directory.Exists(winGetRoot))
                    {
                        foreach (string d in Directory.GetDirectories(winGetRoot, "Gyan.FFmpeg*"))
                        {
                            foreach (string sub in Directory.GetDirectories(d, "ffmpeg-*"))
                            {
                                string cand = Path.Combine(sub, "bin", "ffmpeg.exe");
                                if (File.Exists(cand)) { FfmpegPath = cand; break; }
                            }
                            if (FfmpegPath != null) break;
                        }
                    }
                }
                catch { }
            }
            if (NodeExe == null || FfmpegPath == null || !File.Exists(TtsCliPath)) VoiceEnabled = false;

            try { BuildUI(); }
            catch (Exception ex)
            {
                try { File.AppendAllText(CRASH_LOG, "[UI " + DateTime.Now.ToString("HH:mm:ss") + "] " + ex + "\r\n", Encoding.UTF8); }
                catch { }
                return;
            }

            UpdateAllData();

            // [语音] 预热 VOICEVOX 引擎(后台线程, 不阻塞启动): 聊天时免引擎启动等待
            if (JapaneseMode && VvEngineExe != null && File.Exists(VvCliPath))
            {
                var warmT = new System.Threading.Thread(delegate() { StartVvEngine(); });
                warmT.IsBackground = true;
                warmT.Start();
            }

            BubbleTimer = new DispatcherTimer();
            BubbleTimer.Interval = TimeSpan.FromSeconds(5);
            BubbleTimer.Tick += delegate { InPhrase = false; UpdateAllData(); };

            Timer = new DispatcherTimer();
            Timer.Interval = TimeSpan.FromSeconds(60);
            Timer.Tick += delegate { UpdateAllData(); };
            Timer.Start();

            PhraseTimer = new DispatcherTimer();
            PhraseTimer.Interval = TimeSpan.FromSeconds(45);
            PhraseTimer.Tick += delegate { PhrasePair pr = PickPhrase(); ShowPhrase(pr.Text); if (pr.Expr >= 0 && pr.Expr < ExprImages.Count) WhaleImg.Source = ExprImages[pr.Expr]; };
            PhraseTimer.Start();

            var app = new Application();
            app.DispatcherUnhandledException += delegate(object s, DispatcherUnhandledExceptionEventArgs e)
            {
                try
                {
                    File.AppendAllText(CRASH_LOG, "[Dispatcher " + DateTime.Now.ToString("HH:mm:ss") + "] "
                        + e.Exception + "\r\n", Encoding.UTF8);
                }
                catch { }
                e.Handled = true;
            };
            app.Run(Win);
        }

        static BitmapImage LoadPng(string path)
        {
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.UriSource = new Uri(path);
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }

        static void BuildUI()
        {
            string xaml = @"<Window xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        WindowStyle=""None"" AllowsTransparency=""True"" Background=""Transparent""
        Topmost=""True"" ShowInTaskbar=""False"" Width=""300"" Height=""360"" ResizeMode=""NoResize"">
  <Viewbox x:Name=""Root"" Stretch=""Uniform"">
    <Grid Width=""300"" Height=""360"">
      <Grid.RenderTransform>
        <TranslateTransform x:Name=""JumpTransform"" X=""0"" Y=""0""/>
      </Grid.RenderTransform>
      <Grid.RowDefinitions>
        <RowDefinition Height=""Auto""/>
        <RowDefinition Height=""*""/>
      </Grid.RowDefinitions>
      <Border x:Name=""Bubble"" Grid.Row=""0"" Background=""#F2FFFFFF"" CornerRadius=""12""
              BorderBrush=""#1E3A8A"" BorderThickness=""3"" HorizontalAlignment=""Center""
              Margin=""0,8,0,0"" Padding=""10,5"">
        <StackPanel x:Name=""BubbleTexts"" Width=""180"">
          <TextBlock x:Name=""TxtTitle"" FontSize=""17"" FontWeight=""Bold"" Foreground=""#222222""
                     TextWrapping=""Wrap"" TextAlignment=""Center""/>
          <TextBlock x:Name=""TxtPeak"" FontSize=""13"" FontWeight=""Bold"" Foreground=""#2E7D32""
                     TextWrapping=""Wrap"" TextAlignment=""Center"" Margin=""0,2,0,0""/>
          <TextBlock x:Name=""TxtToday"" FontSize=""12"" Foreground=""#555555""
                     TextWrapping=""Wrap"" TextAlignment=""Center"" Margin=""0,1,0,0""/>
          <TextBlock x:Name=""TxtBalance"" FontSize=""12"" Foreground=""#555555""
                     TextWrapping=""Wrap"" TextAlignment=""Center"" Margin=""0,1,0,0""/>
          <TextBox x:Name=""ChatInput"" FontSize=""12"" Height=""22"" Margin=""0,5,0,0""
                   Background=""#F0FFFFFF"" BorderBrush=""#1E3A8A"" BorderThickness=""1""
                   Visibility=""Collapsed"" ToolTip=""双击鲸鱼聊两句，回车发送""/>
        </StackPanel>
      </Border>
      <Image x:Name=""Whale"" Grid.Row=""1"" Stretch=""Uniform"" Cursor=""Hand""
             HorizontalAlignment=""Center"" VerticalAlignment=""Center"" Margin=""0,2,0,4""/>
    </Grid>
  </Viewbox>
</Window>";
            Win = (Window)System.Windows.Markup.XamlReader.Load(System.Xml.XmlReader.Create(new StringReader(xaml)));
            WhaleImg = (Image)Win.FindName("Whale");
            Jump = (TranslateTransform)Win.FindName("JumpTransform");
            TxtTitle = (TextBlock)Win.FindName("TxtTitle");
            TxtPeak = (TextBlock)Win.FindName("TxtPeak");
            TxtToday = (TextBlock)Win.FindName("TxtToday");
            TxtBal = (TextBlock)Win.FindName("TxtBalance");
            ChatBox = (System.Windows.Controls.TextBox)Win.FindName("ChatInput");
            if (ChatBox != null)
            {
                ChatBox.KeyDown += delegate(object s2, System.Windows.Input.KeyEventArgs e2)
                {
                    if (e2.Key == Key.Enter)
                    {
                        e2.Handled = true;
                        string ask = ChatBox.Text.Trim();
                        if (ask.Length == 0) return;
                        ChatBox.Text = "";
                        ShowPhrase("思考中……(吃口白饭先)");
                        var t2 = new System.Threading.Thread(delegate()
                        {
                            string reply = AskDeepSeek(ask);
                            // [语音] 日语模式: 语音念『』之前的全部日语台词(可能跨多行), 不含中文翻译
                            string speak = reply;
                            if (JapaneseMode)
                            {
                                string jpPart = reply;
                                int bk = reply.IndexOf('『');
                                if (bk >= 0) jpPart = reply.Substring(0, bk);
                                speak = jpPart.Replace("\n", " ").Trim();
                            }
                            Win.Dispatcher.BeginInvoke((Action)(delegate()
                            {
                                // 诊断: 记录完整回复(看中文翻译有没有)
                                try { File.AppendAllText(CRASH_LOG, "[chat " + DateTime.Now.ToString("HH:mm:ss") + "] REPLY=[" + reply + "]\r\n", Encoding.UTF8); } catch { }
                                ShowPhrase(reply.Length > 0 ? reply : "（网络开小差了…）");
                                if (ExprImages.Count > 0) WhaleImg.Source = ExprImages[0];
                            }));
                            // [语音] AI 回复用 TTS 念出来（本线程为后台线程, PlaySync 不卡 UI）
                            SpeakText(speak);
                        });
                        t2.IsBackground = true;
                        t2.Start();
                    }
                };
            }

            // 预加载全部表情
            foreach (string f in ExprFiles)
            {
                string p = Path.Combine(EXPR_DIR, f);
                if (File.Exists(p)) ExprImages.Add(LoadPng(p));
            }
            if (ExprImages.Count == 0)
            {
                MessageBox.Show("no expression images found in " + EXPR_DIR, "desktop-bigfish",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            WhaleImg.Source = ExprImages[0]; // 默认 idle


            Win.MouseLeftButtonDown += delegate(object s, MouseButtonEventArgs e)
            { DownPos = e.GetPosition(Win); DownTime = DateTime.Now; };

            Win.MouseMove += delegate(object s, MouseEventArgs e)
            {
                if (DownPos.HasValue && e.LeftButton == MouseButtonState.Pressed && !IsDragging)
                {
                    Point pos = e.GetPosition(Win);
                    if ((DateTime.Now - DownTime).TotalMilliseconds < 400 &&
                        (Math.Abs(pos.X - DownPos.Value.X) > 4 || Math.Abs(pos.Y - DownPos.Value.Y) > 4))
                    {
                        IsDragging = true;
                        if (ExprImages.Count > 4) WhaleImg.Source = ExprImages[4]; // 害羞
                        try { Win.DragMove(); } catch { }
                    }
                }
            };

            Win.MouseLeftButtonUp += delegate(object s, MouseButtonEventArgs e)
            {
                if (!IsDragging)
                {
                    // [v4] 双击检测：两次点击间隔 < 450ms 且位置接近 → 开/关聊天框
                    Point nowPos = e.GetPosition(Win);
                    bool isDouble = (DateTime.Now - LastClickAt).TotalMilliseconds < 450
                        && Math.Abs(nowPos.X - LastClickPos.X) < 8 && Math.Abs(nowPos.Y - LastClickPos.Y) < 8;
                    LastClickAt = DateTime.Now; LastClickPos = nowPos;
                    if (isDouble && ChatBox != null)
                    {
                        ChatBox.Visibility = ChatBox.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                        if (ChatBox.Visibility == Visibility.Visible) { ChatBox.Focus(); }
                        return;
                    }
                    Bounce();
                    PhrasePair pr = PickPhrase();
                    ShowPhrase(pr.Text);
                    if (pr.Expr >= 0 && pr.Expr < ExprImages.Count) WhaleImg.Source = ExprImages[pr.Expr];
                }
                else
                {
                    WhaleImg.Source = ExprImages[0];
                }
                IsDragging = false;
                DownPos = null;
            };

            var menu = new ContextMenu();
            var miRefresh = new MenuItem(); miRefresh.Header = "刷新数据";
            miRefresh.Click += delegate { InPhrase = false; if (BubbleTimer != null) BubbleTimer.Stop(); UpdateAllData(); };
            var miBig = new MenuItem(); miBig.Header = "放大";
            miBig.Click += delegate { Win.Width = Math.Min(480, Win.Width * 1.2); Win.Height = Win.Width * 360 / 300; };
            var miSmall = new MenuItem(); miSmall.Header = "缩小";
            miSmall.Click += delegate { Win.Width = Math.Max(180, Win.Width / 1.2); Win.Height = Win.Width * 360 / 300; };
            // [语音] 菜单: 模式切换（日语七海/中文萝莉 联动回复语言与声线）+ 语音开关
            var miVoiceSel = new MenuItem();
            miVoiceSel.Header = "模式：" + (JapaneseMode ? "日语（七海语音）" : "中文（萝莉语音）");
            miVoiceSel.IsEnabled = VoiceEnabled;
            miVoiceSel.Click += delegate
            {
                JapaneseMode = !JapaneseMode;
                VoiceName = JapaneseMode ? "ja-JP-NanamiNeural" : "zh-CN-XiaoyiNeural";
                miVoiceSel.Header = "模式：" + (JapaneseMode ? "日语（七海语音）" : "中文（萝莉语音）");
            };
            var miVoice = new MenuItem();
            miVoice.Header = VoiceEnabled ? "语音：开（聊天回复朗读）" : "语音：关（工具缺失）";
            miVoice.IsEnabled = VoiceEnabled;
            miVoice.Click += delegate
            {
                VoiceEnabled = !VoiceEnabled;
                miVoice.Header = VoiceEnabled ? "语音：开（聊天回复朗读）" : "语音：关";
                miVoiceSel.IsEnabled = VoiceEnabled;
            };
            var miExit = new MenuItem(); miExit.Header = "退出";
            miExit.Click += delegate { Win.Close(); };
            menu.Items.Add(miRefresh);
            menu.Items.Add(miBig);
            menu.Items.Add(miSmall);
            menu.Items.Add(miVoice);
            menu.Items.Add(miVoiceSel);
            menu.Items.Add(miExit);
            Win.ContextMenu = menu;
        }

        // [语音] 在 PATH 中查找可执行文件
        static string FindOnPath(string exe)
        {
            try
            {
                string pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
                foreach (string dir in pathVar.Split(';'))
                {
                    if (dir.Length == 0) continue;
                    try
                    {
                        string cand = Path.Combine(dir.Trim('"'), exe);
                        if (File.Exists(cand)) return cand;
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        // [语音] AI 回复朗读: 日语模式优先 VOICEVOX 本地(ずんだもん), 失败回退 edge-tts(七海)
        // 必须由后台线程调用(阻塞); 任何环节缺失/失败都静默降级
        static void SpeakText(string text)
        {
            if (!VoiceEnabled || String.IsNullOrEmpty(text)) return;
            if (NodeExe == null || !File.Exists(TtsCliPath)) return;
            // 诊断: 记录要念的文本(截断 200 字)
            try
            {
                File.AppendAllText(CRASH_LOG, "[voice " + DateTime.Now.ToString("HH:mm:ss") + "] SPEAK len=" + text.Length + " text=[" + (text.Length > 200 ? text.Substring(0, 200) : text) + "]\r\n", Encoding.UTF8);
            }
            catch { }
            string tag = DateTime.Now.ToString("HHmmssfff");
            string tmpDir = Path.Combine(Path.GetTempPath(), "bigfish-voice");
            try { if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir); } catch { return; }
            try
            {
                // A) VOICEVOX 本地合成优先(日语模式且 vv-cli 在): 快、无网络断流
                string wav = null;
                if (JapaneseMode && File.Exists(VvCliPath))
                {
                    wav = SynthVoicevox(text, tmpDir, tag);
                    // 引擎没跑: 自动隐藏拉起 run.exe, 等就绪后重试一次
                    if (wav == null && VvEngineExe != null && StartVvEngine())
                        wav = SynthVoicevox(text, tmpDir, tag);
                    if (wav != null)
                        File.AppendAllText(CRASH_LOG, "[voice " + DateTime.Now.ToString("HH:mm:ss") + "] voicevox OK\r\n", Encoding.UTF8);
                }
                // B) edge-tts 兜底(带时长校验重试; 中文模式也走这里)
                if (wav == null) wav = SynthEdgeTts(text, tmpDir, tag);
                if (wav == null || !File.Exists(wav)) return;
                // 播放(阻塞到播完; 播放期间暂停随机台词/气泡刷新防干扰, 播完恢复)
                lock (PlayLock)
                {
                    try
                    {
                        Win.Dispatcher.Invoke((Action)(delegate()
                        {
                            if (BubbleTimer != null) BubbleTimer.Stop();
                            if (PhraseTimer != null) PhraseTimer.Stop();
                        }));
                    }
                    catch { }
                    DateTime t0 = DateTime.Now;
                    PlayWavMci(wav);
                    File.AppendAllText(CRASH_LOG, "[voice " + DateTime.Now.ToString("HH:mm:ss") + "] played=" + (DateTime.Now - t0).TotalSeconds.ToString("0.00") + "s\r\n", Encoding.UTF8);
                    try
                    {
                        Win.Dispatcher.Invoke((Action)(delegate()
                        {
                            if (BubbleTimer != null) BubbleTimer.Start();
                            if (PhraseTimer != null) PhraseTimer.Start();
                        }));
                    }
                    catch { }
                }
            }
            catch { }
            finally
            {
                // 清理本次合成的临时文件(v{tag}_*)
                try
                {
                    foreach (string f in Directory.GetFiles(tmpDir, "v" + tag + "_*"))
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
                catch { }
            }
        }

        // 隐藏启动 VOICEVOX 引擎 run.exe 并等待就绪(最多 12 秒); 已在跑则直接返回 true
        static bool StartVvEngine()
        {
            try
            {
                // 已就绪?
                try
                {
                    var probe = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:50021/version");
                    probe.Timeout = 1500;
                    using (var resp = (HttpWebResponse)probe.GetResponse()) { return resp.StatusCode == HttpStatusCode.OK; }
                }
                catch { }
                // 隐藏启动引擎(无窗口, 后台服务)
                var psi = new ProcessStartInfo(VvEngineExe);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                try { Process.Start(psi); } catch { return false; }
                // 轮询等待就绪
                for (int i = 0; i < 12; i++)
                {
                    System.Threading.Thread.Sleep(1000);
                    try
                    {
                        var probe = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:50021/version");
                        probe.Timeout = 1500;
                        using (var resp = (HttpWebResponse)probe.GetResponse()) { return resp.StatusCode == HttpStatusCode.OK; }
                    }
                    catch { }
                }
            }
            catch { }
            return false;
        }

        // VOICEVOX 本地合成(ずんだもん): node vv-cli.mjs <speaker> <wav>, 文本走 stdin(UTF-8)
        // 成功返回 wav 路径, 失败返回 null
        static string SynthVoicevox(string text, string tmpDir, string tag)
        {
            try
            {
                string wav = Path.Combine(tmpDir, "v" + tag + "_vv.wav");
                var psi = new ProcessStartInfo(NodeExe, "\"" + VvCliPath + "\" " + VvSpeaker + " \"" + wav + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardInput = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    using (var sw = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)))
                    {
                        sw.Write(text);
                    }
                    if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return null; }
                    p.StandardOutput.ReadToEnd();
                }
                if (File.Exists(wav) && new FileInfo(wav).Length > 2000) return wav;
                try { if (File.Exists(wav)) File.Delete(wav); } catch { }
            }
            catch { }
            return null;
        }

        // edge-tts 合成兜底: node tts-cli -> mp3, ffmpeg -> wav, 时长校验重试(最多3次)
        // edge-tts WebSocket 偶发断流只返回开头几个字, 用时长校验兜底重来
        static string SynthEdgeTts(string text, string tmpDir, string tag)
        {
            if (FfmpegPath == null) return null;
            double expectSec = text.Length / 6.5;   // 日语语速估约 6.5 字/秒
            for (int attempt = 1; attempt <= 3; attempt++)
            {
                string mp3 = Path.Combine(tmpDir, "v" + tag + "_e" + attempt + ".mp3");
                string wav = Path.Combine(tmpDir, "v" + tag + "_e" + attempt + ".wav");
                try { if (File.Exists(mp3)) File.Delete(mp3); if (File.Exists(wav)) File.Delete(wav); } catch { }
                try
                {
                    var psi = new ProcessStartInfo(NodeExe, "\"" + TtsCliPath + "\" " + VoiceName + " \"" + mp3 + "\"");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.RedirectStandardError = true;
                    using (Process p = Process.Start(psi))
                    {
                        // 关键: StandardInput 默认按系统 ANSI(GBK)编码, 日语假名会被破坏!
                        using (var sw = new StreamWriter(p.StandardInput.BaseStream, new UTF8Encoding(false)))
                        {
                            sw.Write(text);
                        }
                        if (!p.WaitForExit(30000)) { try { p.Kill(); } catch { } return null; }
                        p.StandardOutput.ReadToEnd();
                    }
                    if (!File.Exists(mp3) || new FileInfo(mp3).Length == 0) return null;
                    var psi2 = new ProcessStartInfo(FfmpegPath, "-y -v error -i \"" + mp3 + "\" \"" + wav + "\"");
                    psi2.UseShellExecute = false;
                    psi2.CreateNoWindow = true;
                    using (Process p2 = Process.Start(psi2))
                    {
                        if (!p2.WaitForExit(30000)) { try { p2.Kill(); } catch { } return null; }
                    }
                    if (!File.Exists(wav)) return null;
                    double dur = GetWavDuration(wav);
                    if (dur > 0 && expectSec > 1.5 && dur < expectSec * 0.5) { continue; } // 截断, 重试
                    return wav;
                }
                catch { return null; }
            }
            return null;
        }

        // winmm MCI 播放 wav: open -> play wait -> close（不依赖消息泵, 播放完整）
        [DllImport("winmm.dll", CharSet = CharSet.Auto)]
        static extern int mciSendString(string command, StringBuilder ret, int retSize, IntPtr hwnd);
        static void PlayWavMci(string wavPath)
        {
            try
            {
                if (mciSendString("open \"" + wavPath + "\" type waveaudio alias bf", null, 0, IntPtr.Zero) != 0) return;
                try
                {
                    mciSendString("play bf wait", null, 0, IntPtr.Zero);
                }
                finally
                {
                    mciSendString("close bf", null, 0, IntPtr.Zero);
                }
            }
            catch { }
        }

        // 用 ffmpeg -i 探测 wav 时长(秒), 失败返回 -1
        static double GetWavDuration(string wavPath)
        {
            try
            {
                var psiD = new ProcessStartInfo(FfmpegPath, "-i \"" + wavPath + "\"");
                psiD.UseShellExecute = false; psiD.CreateNoWindow = true;
                psiD.RedirectStandardOutput = true; psiD.RedirectStandardError = true;
                using (Process pd = Process.Start(psiD))
                {
                    string err = pd.StandardError.ReadToEnd();
                    pd.WaitForExit(5000);
                    Match md = Regex.Match(err, "Duration:\\s*(\\d+):(\\d+):(\\d+\\.?\\d*)");
                    if (md.Success)
                        return int.Parse(md.Groups[1].Value) * 3600 + int.Parse(md.Groups[2].Value) * 60
                            + double.Parse(md.Groups[3].Value, CultureInfo.InvariantCulture);
                }
            }
            catch { }
            return -1;
        }

        static void GetPeakInfo(out bool peak, out string label, out string hint)
        {
            int h = DateTime.Now.Hour;
            peak = (h >= 9 && h < 12) || (h >= 14 && h < 18);
            if (peak) { label = "高峰时段"; hint = "原价，省着点用"; }
            else { label = "空闲时段"; hint = "半价，随便造"; }
        }

        static void GetTodayUsage(out long tokens, out double cost)
        {
            tokens = 0; cost = 0.0;
            try
            {
                if (String.IsNullOrEmpty(ZSTD) || !File.Exists(ZSTD) || !Directory.Exists(SESS_ROOT)) return;
                string tmpDir = Path.Combine(Path.GetTempPath(), "bigfish-usage");
                if (!Directory.Exists(tmpDir)) Directory.CreateDirectory(tmpDir);
                DateTime startOfDay = DateTime.Today;
                string[] zstds = Directory.GetFiles(SESS_ROOT, "session.jsonl.zstd", SearchOption.AllDirectories);
                // 只处理今天修改的，按最新优先，最多 6 个（防止扫到旧文件浪费配额）
                var targets = zstds
                    .Select(z => new FileInfo(z))
                    .Where(fi => fi.LastWriteTime >= startOfDay && fi.Length <= 30L * 1024 * 1024)
                    .OrderByDescending(fi => fi.LastWriteTime)
                    .Take(6)
                    .ToList();
                foreach (FileInfo fi in targets)
                {
                    string z = fi.FullName;
                    try
                    {
                        string outFile = Path.Combine(tmpDir,
                            new DirectoryInfo(Path.GetDirectoryName(z)).Name + ".jsonl");
                        var psi = new ProcessStartInfo(ZSTD, "-d \"" + z + "\" -o \"" + outFile + "\" -f");
                        psi.UseShellExecute = false;
                        psi.CreateNoWindow = true;
                        Process p = Process.Start(psi);
                        p.WaitForExit(30000);
                        if (File.Exists(outFile))
                        {
                            using (StreamReader r = new StreamReader(outFile))
                            {
                                string line;
                                while ((line = r.ReadLine()) != null)
                                {
                                    if (line.Contains("\"type\":\"assistant/chunk\"") && line.Contains("\"type\":\"usage\""))
                                    {
                                        try
                                        {
                                            Match mt = Regex.Match(line, "\"time\"\\s*:\\s*(\\d+)");
                                            if (!mt.Success) continue;
                                            if (DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(mt.Groups[1].Value)).LocalDateTime < DateTime.Today) continue;
                                            long i = GetNum(line, "inputTokens");
                                            long o = GetNum(line, "outputTokens");
                                            long ca = GetNum(line, "cacheReadTokens");
                                            tokens += i + o + ca;
                                            cost += (ca * 0.15 + i * 4.5 + o * 13.5) / 1000000.0;
                                        }
                                        catch { }
                                    }
                                }
                            }
                            try { File.Delete(outFile); } catch { }
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        static long GetNum(string line, string key)
        {
            Match m = Regex.Match(line, "\"" + key + "\"\\s*:\\s*(-?\\d+)");
            if (m.Success) return long.Parse(m.Groups[1].Value);
            return 0;
        }

        static string GetBalance()
        {
            if (String.IsNullOrEmpty(ApiKey)) return "--";
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://api.deepseek.com/user/balance");
                req.Method = "GET";
                req.Headers["Authorization"] = "Bearer " + ApiKey;
                req.Timeout = 6000;
                req.ReadWriteTimeout = 6000;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    Match m = Regex.Match(json, "\"total_balance\"\\s*:\\s*\"?([0-9.]+)\"?");
                    if (m.Success)
                        return double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture).ToString("N2", CultureInfo.InvariantCulture);
                    return "err";
                }
            }
            catch { return "err"; }
        }

        // [v4] AI 聊天：调 DeepSeek chat/completions（便宜 flash 模型，人设=大肥鱼傲娇甜，不吃涩涩）
        static string AskDeepSeek(string userText)
        {
            if (String.IsNullOrEmpty(ApiKey)) return "没配 DEEPSEEK_API_KEY 呢，吃不上白饭…";
            try
            {
                // [语音] 日语模式: AI 用日语台词回复(第一行), 第二行『』中文翻译; 中文模式正常中文
                // 注意: ① 只改 sys 不够——flash 跟随用户语言, user 消息必须注入日语指令前缀
                //       ② 光说"加翻译"不够——flash 偶尔忘, 需 few-shot 示例带一个完整例子
                string sys = JapaneseMode
                    ? "あなたは「大肥鱼(おおひれうお)」という名のデスクトップペット、クジラ娘メイド。ツンデレで甘えん坊。口調は短くカジュアル、自分は「あたし/このクジラちゃん」。健康で楽しい話題のみ、際どい内容は禁止。ご飯を食べてないと力が出ない設定。\n【返答形式・絶対厳守】1行目に日本語の台詞だけを書く。2行目の『』の中に中国語訳を必ず書く。中国語訳を忘れてはいけない！"
                    : "你是大肥鱼，一只傲娇甜的鲸鱼娘桌宠。说话简短口语化，自称大肥鱼/本鱼，会傲娇但热心。只聊健康内容，不碰涩涩不越界。偶尔提没干饭没力气。";
                string userMsg = JapaneseMode ? "[日本語で返答してください。中国語禁止]\n" + userText : userText;
                string body;
                if (JapaneseMode)
                {
                    // few-shot: 给一个"日语台词+『中文翻译』"的完整示例, 提高格式遵守率
                    body = "{\"model\":\"deepseek-v4-flash\",\"messages\":["
                        + "{\"role\":\"system\",\"content\":" + JsonEsc(sys) + "},"
                        + "{\"role\":\"user\",\"content\":" + JsonEsc("[日本語で返答してください。中国語禁止]\nお腹すいたなあ") + "},"
                        + "{\"role\":\"assistant\",\"content\":" + JsonEsc("お腹すいたの？じゃあ、一緒にご飯食べに行こ！あたしがいいお店知ってるんだ。\n『饿了吗？那一起去吃饭吧！我知道一家好店。』") + "},"
                        + "{\"role\":\"user\",\"content\":" + JsonEsc(userMsg) + "}],\"max_tokens\":600,\"stream\":false}";
                }
                else
                {
                    body = "{\"model\":\"deepseek-v4-flash\",\"messages\":["
                        + "{\"role\":\"system\",\"content\":" + JsonEsc(sys) + "},"
                        + "{\"role\":\"user\",\"content\":" + JsonEsc(userMsg) + "}],\"max_tokens\":600,\"stream\":false}";
                }
                var req = (HttpWebRequest)WebRequest.Create("https://api.deepseek.com/chat/completions");
                req.Method = "POST";
                req.ContentType = "application/json";
                req.Headers["Authorization"] = "Bearer " + ApiKey;
                req.Timeout = 30000;
                req.ReadWriteTimeout = 30000;
                byte[] data = Encoding.UTF8.GetBytes(body);
                req.ContentLength = data.Length;
                using (Stream ws = req.GetRequestStream()) { ws.Write(data, 0, data.Length); }
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    Match m = Regex.Match(json, "\"content\"\\s*:\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
                    if (m.Success)
                    {
                        string txt = m.Groups[1].Value
                            .Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\").Replace("\\u003c", "<").Replace("\\u003e", ">");
                        if (txt.Length > 600) txt = txt.Substring(0, 600) + "…";
                        return txt;
                    }
                    return "（没读懂大肥鱼的回话…）";
                }
            }
            catch (Exception ex) { return "（AI 掉线了：" + ex.Message + "）"; }
        }

        static string JsonEsc(string s)
        {
            if (s == null) return "\"\"";
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                if (c == '"') sb.Append("\\\"");
                else if (c == '\\') sb.Append("\\\\");
                else if (c == '\n') sb.Append("\\n");
                else if (c == '\r') { }
                else if (c < 32) sb.Append("\\u" + ((int)c).ToString("x4"));
                else sb.Append(c);
            }
            sb.Append("\"");
            return sb.ToString();
        }

        static void UpdateAllData()
        {
            bool peak; string plabel, phint;
            GetPeakInfo(out peak, out plabel, out phint);
            if (!InPhrase)
            {
                TxtTitle.Text = "大肥鱼待命中~";
                TxtPeak.Text = plabel;
                Color c = peak ? Color.FromArgb(255, 230, 126, 34) : Color.FromArgb(255, 46, 125, 50);
                TxtPeak.Foreground = new SolidColorBrush(c);
                TxtBal.Text = "余额 查询中…";
                TxtToday.Text = "今日消耗 统计中…";
            }
            // 余额 + 用量全部后台线程，且用量结果缓存 5 分钟（不反复解压日志）
            var t = new System.Threading.Thread(delegate()
            {
                System.Threading.Thread.Sleep(2000); // 窗口先渲染，统计延后
                string bal = "err";
                try { bal = GetBalance(); } catch { }
                string cost;
                lock (StatLock)
                {
                    if (CachedCost != null && (DateTime.Now - CachedAt).TotalMinutes < 5) cost = CachedCost;
                    else cost = null;
                }
                if (cost == null)
                {
                    long tokens; double c2;
                    try { GetTodayUsage(out tokens, out c2); cost = "今日消耗 ¥" + c2.ToString("0.##", CultureInfo.InvariantCulture); }
                    catch { cost = "今日消耗 ¥--"; }
                    lock (StatLock) { CachedCost = cost; CachedAt = DateTime.Now; }
                }
                try
                {
                    Win.Dispatcher.BeginInvoke((Action)(delegate()
                    {
                        if (!InPhrase)
                        {
                            TxtBal.Text = "余额 ¥" + bal;
                            TxtToday.Text = cost;
                        }
                    }));
                }
                catch { }
            });
            t.IsBackground = true;
            t.Start();
        }

        static void ShowPhrase(string text)
        {
            InPhrase = true;
            TxtTitle.Text = text;
            TxtPeak.Text = "";
            TxtToday.Text = "";
            TxtBal.Text = "";
            BubbleTimer.Stop();
            BubbleTimer.Start();
        }

        // [v4] 洗牌袋取台词：装满索引洗牌，一轮内不重复（模仿病娇桌宠 PickLine）
        static PhrasePair PickPhrase()
        {
            if (PhraseBag.Count == 0)
            {
                for (int i = 0; i < PhrasePairs.Length; i++) PhraseBag.Add(i);
                // Fisher-Yates 洗牌
                for (int i = PhraseBag.Count - 1; i > 0; i--)
                {
                    int j = rnd.Next(i + 1);
                    int t = PhraseBag[i]; PhraseBag[i] = PhraseBag[j]; PhraseBag[j] = t;
                }
            }
            int idx = PhraseBag[PhraseBag.Count - 1];
            PhraseBag.RemoveAt(PhraseBag.Count - 1);
            PhrasePair pr = PhrasePairs[idx];
            pr.Expr = PoseForText(pr.Text, pr.Expr); // 表情按内容再匹配一次
            return pr;
        }

        // [v4] 根据台词关键词智能匹配表情（模仿病娇桌宠 PoseForLine）
        static int PoseForText(string text, int fallback)
        {
            if (text.IndexOf("炸毛") >= 0 || text.IndexOf("罢工") >= 0 || text.IndexOf("咬你") >= 0
                || text.IndexOf("吃醋") >= 0 || text.IndexOf("闹别扭") >= 0 || text.IndexOf("专属") >= 0) return 7; // 生气/吃醋
            if (text.IndexOf("吓") >= 0 || text.IndexOf("胆子小") >= 0) return 2;                              // 害怕
            if (text.IndexOf("什么") >= 0 || text.IndexOf("诶") >= 0 || text.IndexOf("?!") >= 0) return 3;    // 震惊
            if (text.IndexOf("好耶") >= 0 || text.IndexOf("元气") >= 0 || text.IndexOf("夸") >= 0
                || text.IndexOf("贴贴") >= 0 || text.IndexOf("爱听") >= 0 || text.IndexOf("口袋里") >= 0) return 0; // 开心
            if (text.IndexOf("呜") >= 0 || text.IndexOf("被抓包") >= 0 || text.IndexOf("脸红") >= 0
                || text.IndexOf("害羞") >= 0 || text.IndexOf("靠那么近") >= 0 || text.IndexOf("蹭蹭") >= 0
                || text.IndexOf("摸头") >= 0) return 5;                                                     // 无奈/害羞
            if (text.IndexOf("才") >= 0 || text.IndexOf("哼") >= 0 || text.IndexOf("笨蛋") >= 0) return 6;    // 哭笑不得/傲娇
            if (fallback >= 0 && fallback < ExprImages.Count) return fallback;
            return 0;
        }

        static void Bounce()
        {
            var a = new DoubleAnimation();
            a.From = 0.0; a.To = -12.0;
            a.Duration = TimeSpan.FromMilliseconds(90);
            a.AutoReverse = true;
            a.RepeatBehavior = new RepeatBehavior(2);
            a.FillBehavior = FillBehavior.Stop;
            a.Completed += delegate { Jump.Y = 0.0; Jump.BeginAnimation(TranslateTransform.YProperty, null); };
            Jump.BeginAnimation(TranslateTransform.YProperty, a);
        }
    }
}
