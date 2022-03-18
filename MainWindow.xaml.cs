using System;
using System.Windows;
using System.Windows.Forms;
using System.Speech.Recognition;
namespace GoogleAssistantWindows
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int NormalHeight = 100;
        private const int DebugHeight = 350;

        private readonly UserManager _userManager;
        private readonly Assistant _assistant;

        private readonly KeyboardHook _hook;

        private readonly NotifyIcon _notifyIcon;

        private readonly AudioOut _audioOut;

        private AssistantState _assistantState = AssistantState.Inactive;

        private static SpeechRecognitionEngine engine;
        public bool enableSpeechDetection = true;
        private string[] WakeUpWords = { "hey friday", "Friday", "jarvis", "eight Friday","five","hey five", "eight write a","eight write","eight rival","write a","writing","about a","ride a","a Friday","eight write a","hay Friday","be Friday","851","858","to write a","ID","knee Friday"};
        public MainWindow()
        {
            void StartListening()
            {
                if (_assistant.IsInitialised() && _assistantState == AssistantState.Inactive)
                {
                    _assistant.NewConversation();
                    _audioOut.PlayNotification();
                }
            }

            InitializeComponent();

            _audioOut = new AudioOut();

            _hook = new KeyboardHook();
            _hook.KeyDown += OnHookKeyDown;
            void OnHookKeyDown(object sender, HookEventArgs e)
            {
                // Global keyboard hook for Ctrl+Alt+G to start listening.
                if (e.Control && e.Alt && e.Key == Keys.G)
                {
                    if (_assistantState == AssistantState.Inactive)
                    {
                        StartListening();
                    }
                    else
                    {
                        _assistant.StopRecording(true);
                        _assistant.ignore = true;
                    }
                }
            }

            // When minimized it will hide in the tray. but the global keyboard hook should still work
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Icon = new System.Drawing.Icon("friday.ico");
            _notifyIcon.Text = Title;
            _notifyIcon.DoubleClick +=
                delegate
                {
                    _notifyIcon.Visible = false;
                    Show();
                    WindowState = WindowState.Normal;
                };

            _assistant = new Assistant();
            _assistant.OnDebug += Output;
            _assistant.OnAssistantStateChanged += OnAssistantStateChanged;


            _userManager = UserManager.Instance;
            _userManager.OnUserUpdate += OnUserUpdate;


            if (enableSpeechDetection)    //voice detection
            {
                engine = new SpeechRecognitionEngine(new System.Globalization.CultureInfo("en-US"));
                engine.SetInputToDefaultAudioDevice();

                DictationGrammar customDictationGrammar = new DictationGrammar();
                customDictationGrammar.Name = "question dictation";
                customDictationGrammar.Enabled = true;
                engine.LoadGrammar(customDictationGrammar);
                foreach (string word in WakeUpWords)
                {
                    customDictationGrammar.SetDictationContext(word, null);
                }
               
                engine.RecognizeAsync(RecognizeMode.Multiple);
                engine.SpeechRecognized += rec;
                Output("Started Speech Recogniton ", false);
            }

            void rec(object sender, SpeechRecognizedEventArgs result)
            {
                if (_assistantState == AssistantState.Inactive)
                {
                    Output("Detected speech: " + result.Result.Text, false);
                    foreach (string word in WakeUpWords)
                    {
                        if (word == result.Result.Text)
                        {
                            StartListening();
                        }
                    }
                }
            }
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                _notifyIcon.Visible = true;
                Hide();
            }
            base.OnStateChanged(e);
        }

        private void OnAssistantStateChanged(AssistantState state)
        {
            _assistantState = state;
            UpdateButtonText(state);
        }

        private void UpdateButtonText(AssistantState state)
        {
            if (ButtonRecord.Dispatcher.CheckAccess())
                ButtonRecord.Content = state == AssistantState.Inactive ? "Press" : state.ToString();
            else
                ButtonRecord.Dispatcher.BeginInvoke(new Action(() => UpdateButtonText(state)));
        }

        private void OnUserUpdate(UserManager.GoogleUserData userData)
        {
            ButtonRecord.IsEnabled = false;
            _assistant.Shutdown();
            if (userData != null)
            {
                _assistant.InitAssistantForUser(_userManager.GetChannelCredential());
                ButtonRecord.IsEnabled = true;
            }
        }

        private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            if (Utils.HasTokenFile())
                _userManager.GetOrRefreshCredential();     // we don't need to wait for this UserManager will throw an event on loaded.       
        }

        private void ButtonRecord_OnClick(object sender, RoutedEventArgs e)
        {
            StartListening();
        }

        private void StartListening()
        {
            if (_assistant.IsInitialised() && _assistantState == AssistantState.Inactive)
            {
                _assistant.NewConversation();
                _audioOut.PlayNotification();
            }
        }

        public void Output(string output, bool consoleOnly = false)
        {
            if (consoleOnly)
            {
                System.Diagnostics.Debug.WriteLine(output);
                return;
            }

            if (ListBoxOutput.Dispatcher.CheckAccess())
            {
                System.Diagnostics.Debug.WriteLine(output);

                // stop using memory for old debug lines.
                if (ListBoxOutput.Items.Count > 500)
                    ListBoxOutput.Items.RemoveAt(0);

                ListBoxOutput.Items.Add(output);
                ListBoxOutput.ScrollIntoView(ListBoxOutput.Items[ListBoxOutput.Items.Count - 1]);

                if (output.StartsWith("Error") && Height == NormalHeight)
                    Height = DebugHeight;
            }
            else
                ListBoxOutput.Dispatcher.BeginInvoke(new Action(() => Output(output)));
        }

        private void DebugButton_OnClick(object sender, RoutedEventArgs e)
        {
            Height = (Height == NormalHeight ? DebugHeight : NormalHeight);
        }

        private void GoogleAccountControl_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}
