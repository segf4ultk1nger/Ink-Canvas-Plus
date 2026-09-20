using iNKORE.UI.WPF.Modern.Controls;
using InkCanvasPlus.Services;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace InkCanvasPlus
{
    /// <summary>
    /// Interaction logic for RandWindow.xaml
    /// </summary>
    public partial class RandWindow : Window
    {
        private readonly DispatcherGate _ui;
        private DispatcherTimer _autoStartTimer;
        private DispatcherTimer _randAnimTimer;
        private DispatcherTimer _randAutoCloseTimer;

        public RandWindow()
        {
            InitializeComponent();
            _ui = new DispatcherGate(Dispatcher);
            Closed += RandWindow_Closed;
        }

        public RandWindow(bool IsAutoClose)
        {
            InitializeComponent();
            _ui = new DispatcherGate(Dispatcher);
            Closed += RandWindow_Closed;

            isAutoClose = IsAutoClose;

            _autoStartTimer = _ui.RunOnce(TimeSpan.FromMilliseconds(100), () =>
            {
                BorderBtnRand_MouseUp(BorderBtnRand, null);
            });
        }

        public static int randSeed = 0;

        public bool isAutoClose = false;

        public int TotalCount = 1;
        public int PeopleCount = 60;
        public List<string> Names = new List<string>();

        private void RandWindow_Closed(object sender, EventArgs e)
        {
            _autoStartTimer?.Stop();
            _randAnimTimer?.Stop();
            _randAutoCloseTimer?.Stop();
        }

        private void BorderBtnAdd_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TotalCount >= PeopleCount) return;
            TotalCount++;
            LabelNumberCount.Content = TotalCount.ToString();
            SymbolIconStart.Symbol = Symbol.People;
        }

        private void BorderBtnMinus_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TotalCount < 2) return;
            TotalCount--;
            LabelNumberCount.Content = TotalCount.ToString();
            if (TotalCount == 1)
            {
                SymbolIconStart.Symbol = Symbol.Contact;
            }
        }

        private void BorderBtnRand_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Random random = new Random();// randSeed + DateTime.Now.Millisecond / 10 % 10);
            List<int> rands = new List<int>();

            LabelOutput2.Visibility = Visibility.Collapsed;
            LabelOutput3.Visibility = Visibility.Collapsed;
            BorderBtnRandCover.Visibility = Visibility.Visible;

            int animationStep = 0;
            ShowRandPreview(random, rands);

            _randAnimTimer?.Stop();
            _randAnimTimer = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _randAnimTimer.Tick += (s, ev) =>
            {
                animationStep++;
                if (animationStep < 5)
                {
                    ShowRandPreview(random, rands);
                    return;
                }

                _randAnimTimer.Stop();
                FinishRand(random);
            };
            _randAnimTimer.Start();
        }

        private void ShowRandPreview(Random random, List<int> rands)
        {
            int rand = random.Next(1, PeopleCount + 1);
            while (rands.Contains(rand))
            {
                rand = random.Next(1, PeopleCount + 1);
            }
            rands.Add(rand);
            if (rands.Count >= PeopleCount) rands.Clear();
            if (Names.Count != 0)
            {
                LabelOutput.Content = Names[rand - 1];
            }
            else
            {
                LabelOutput.Content = rand.ToString();
            }
        }

        private void FinishRand(Random random)
        {
            string outputString = "";
            List<string> outputs = new List<string>();
            List<int> rands = new List<int>();

            for (int i = 0; i < TotalCount; i++)
            {
                int rand = random.Next(1, PeopleCount + 1);
                while (rands.Contains(rand))
                {
                    rand = random.Next(1, PeopleCount + 1);
                }
                rands.Add(rand);
                if (rands.Count >= PeopleCount) rands.Clear();

                if (Names.Count != 0)
                {
                    outputs.Add(Names[rand - 1]);
                    outputString += Names[rand - 1] + Environment.NewLine;
                }
                else
                {
                    outputs.Add(rand.ToString());
                    outputString += rand.ToString() + Environment.NewLine;
                }
            }
            if (TotalCount <= 5)
            {
                LabelOutput.Content = outputString.ToString().Trim();
            }
            else if (TotalCount <= 10)
            {
                LabelOutput2.Visibility = Visibility.Visible;
                outputString = "";
                for (int i = 0; i < (outputs.Count + 1) / 2; i++)
                {
                    outputString += outputs[i].ToString() + Environment.NewLine;
                }
                LabelOutput.Content = outputString.ToString().Trim();
                outputString = "";
                for (int i = (outputs.Count + 1) / 2; i < outputs.Count; i++)
                {
                    outputString += outputs[i].ToString() + Environment.NewLine;
                }
                LabelOutput2.Content = outputString.ToString().Trim();
            }
            else
            {
                LabelOutput2.Visibility = Visibility.Visible;
                LabelOutput3.Visibility = Visibility.Visible;
                outputString = "";
                for (int i = 0; i < (outputs.Count + 1) / 3; i++)
                {
                    outputString += outputs[i].ToString() + Environment.NewLine;
                }
                LabelOutput.Content = outputString.ToString().Trim();
                outputString = "";
                for (int i = (outputs.Count + 1) / 3; i < (outputs.Count + 1) * 2 / 3; i++)
                {
                    outputString += outputs[i].ToString() + Environment.NewLine;
                }
                LabelOutput2.Content = outputString.ToString().Trim();
                outputString = "";
                for (int i = (outputs.Count + 1) * 2 / 3; i < outputs.Count; i++)
                {
                    outputString += outputs[i].ToString() + Environment.NewLine;
                }
                LabelOutput3.Content = outputString.ToString().Trim();
            }
            BorderBtnRandCover.Visibility = Visibility.Collapsed;

            if (isAutoClose)
            {
                _randAutoCloseTimer?.Stop();
                _randAutoCloseTimer = _ui.RunOnce(TimeSpan.FromMilliseconds(1500), Close);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Names = new List<string>();
            if (File.Exists(App.RootPath + "Names.txt"))
            {
                string[] fileNames = File.ReadAllLines(App.RootPath + "Names.txt");
                string[] replaces = new string[0];

                if (File.Exists(App.RootPath + "Replace.txt"))
                {
                    replaces = File.ReadAllLines(App.RootPath + "Replace.txt");
                }

                //Fix emtpy lines
                foreach (string str in fileNames)
                {
                    string s = str;
                    //Make replacement
                    foreach (string replace in replaces)
                    {
                        if (s == Strings.Left(replace, replace.IndexOf("-->")))
                        {
                            s = Strings.Mid(replace, replace.IndexOf("-->") + 4);
                        }
                    }

                    if (s != "") Names.Add(s);
                }

                PeopleCount = Names.Count();
                TextBlockPeopleCount.Text = PeopleCount.ToString();
                if (PeopleCount == 0)
                {
                    PeopleCount = 60;
                    TextBlockPeopleCount.Text = "点击此处以导入名单";
                }
            }
        }

        private void BorderBtnHelp_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //MessageBox.Show("如需显示姓名，请在程序目录下新建 Names.txt，并将姓名输入，一行一个。");
            new NamesInputWindow().ShowDialog();
            Window_Loaded(this, null);
        }

        private void BtnClose_MouseUp(object sender, MouseButtonEventArgs e)
        {
            Close();
        }
    }
}
