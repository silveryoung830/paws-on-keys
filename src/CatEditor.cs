using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Diagnostics;
using Forms = System.Windows.Forms;

namespace PawsOnKeys
{
    public sealed class CatEditor : Window
    {
        public CatLook Result { get; private set; }

        private readonly CatLook draft;
        private readonly PetView preview;
        private readonly Button fastTypingButton;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly StackPanel controls = new StackPanel();
        private readonly Image photo = new Image
        {
            Stretch = Stretch.Uniform,
            Height = 140
        };
        private readonly Border photoHolder = new Border();
        private readonly ComboBox samplePart = new ComboBox();
        private readonly Button[] colors = new Button[3];
        private BitmapSource sourcePhoto;
        private double previous, moveUntil;
        private double fastTypingUntil, nextDemoPress;
        private int sampleKey;
        private const string DemoKeys = "ASDFJKL";
        private static readonly Brush Paper = Paint("#F8F6F1"), Ink = Paint("#292C28"), Green = Paint("#31573F"), Line = Paint("#DCDDD2");
        private static Brush Paint(string color)
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        }

        public CatEditor(CatLook initial)
        {
            draft = initial.Copy();
            draft.Normalize();
            Title = "我的猫 · Paws on Keys";
            FontFamily = new FontFamily("Microsoft YaHei UI, Segoe UI");
            FontSize = 13;
            Foreground = Ink;
            Background = Paper;
            Width = Math.Min(850, SystemParameters.WorkArea.Width - 24);
            Height = Math.Min(660, SystemParameters.WorkArea.Height - 24);
            MinWidth = Math.Min(540, Width);
            MinHeight = Math.Min(400, Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            var root = new DockPanel
            {
                Margin = new Thickness(20, 14, 20, 14)
            };
            Content = root;
            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            top.Children.Add(new TextBlock { Text = "我的猫", FontSize = 29, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) });
            var entry = new WrapPanel();
            top.Children.Add(entry);
            entry.Children.Add(ButtonFor("从照片开始…", OpenPhoto));
            entry.Children.Add(ButtonFor("手动捏猫", delegate
            {
                photoHolder.Visibility = Visibility.Collapsed;
                controls.BringIntoView();
            }));
            var bottom = new DockPanel
            {
                Margin = new Thickness(0, 10, 0, 0)
            };
            DockPanel.SetDock(bottom, Dock.Bottom);
            root.Children.Add(bottom);
            var apply = ButtonFor("放到桌面", delegate
            {
                Result = draft.Copy();
                Result.Normalize();
                DialogResult = true;
            });
            apply.Background = Green;
            apply.Foreground = Brushes.White;
            apply.FontWeight = FontWeights.Bold;
            apply.Padding = new Thickness(22, 8, 22, 8);
            DockPanel.SetDock(apply, Dock.Right);
            bottom.Children.Add(apply);
            bottom.Children.Add(new TextBlock
            {
                Text = "桌角猫在本机读取当前键位来播放动画。不保存按键历史，也不上传按键。",
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 3, 12, 3),
                Foreground = Paint("#526052"),
                ToolTip = "右键托盘猫图标可暂停键鼠联动。单独隐藏上浮键名时，猫仍会跟着敲键。"
            });
            var main = new Grid
            {
                Margin = new Thickness(0, 10, 0, 0)
            };
            root.Children.Add(main);
            main.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(255), MaxWidth = 300 });
            main.ColumnDefinitions.Add(new ColumnDefinition());
            var editScroll = new ScrollViewer
            {
                Content = controls,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new Thickness(0, 0, 10, 0)
            };
            main.Children.Add(editScroll);
            var stage = new DockPanel
            {
                Margin = new Thickness(10, 0, 0, 0)
            };
            Grid.SetColumn(stage, 1);
            main.Children.Add(stage);
            var buttons = new WrapPanel
            {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            DockPanel.SetDock(buttons, Dock.Bottom);
            stage.Children.Add(buttons);
            preview = new PetView
            {
                Look = draft
            };
            buttons.Children.Add(ButtonFor("敲键", DemoKeyPress));
            buttons.Children.Add(ButtonFor("踩空格", delegate { preview.PressKey(32, "Space"); }));
            fastTypingButton = ButtonFor("试试快打", delegate { SetFastTyping(fastTypingUntil == 0); });
            fastTypingButton.ToolTip = "连续演示 9 秒，再点一次可提前停止";
            buttons.Children.Add(fastTypingButton);
            buttons.Children.Add(ButtonFor("推鼠标", delegate
            {
                moveUntil = clock.Elapsed.TotalSeconds + 1.7;
            }));
            buttons.Children.Add(ButtonFor("摸头", preview.PetHead));
            var canvas = new Border
            {
                Background = Paint("#DCDCD6"),
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8),
                Child = preview
            };
            canvas.MouseLeftButtonDown += delegate (object sender, System.Windows.Input.MouseButtonEventArgs args)
            {
                if (preview.IsHead(args.GetPosition(preview)))
                {
                    preview.PetHead();
                    args.Handled = true;
                }
            };
            stage.Children.Add(canvas);
            BuildPhoto();
            BuildControls();
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            timer.Tick += OnFrame;
            Loaded += delegate
            {
                previous = clock.Elapsed.TotalSeconds;
                timer.Start();
            };
            Closed += delegate
            {
                timer.Stop();
                photo.Source = null;
                sourcePhoto = null;
            };
        }

        private static Button ButtonFor(string label, Action click)
        {
            var b = new Button
            {
                Content = label,
                Padding = new Thickness(11, 5, 11, 5),
                Margin = new Thickness(0, 2, 6, 3),
                Background = Brushes.White,
                Foreground = Ink,
                BorderBrush = Line,
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand
            };
            b.Click += delegate
            {
                click();
            };
            return b;
        }

        private void Heading(string text)
        {
            controls.Children.Add(new TextBlock { Text = text, Margin = new Thickness(0, 8, 0, 3), FontWeight = FontWeights.SemiBold });
        }

        private void BuildPhoto()
        {
            var pan = new StackPanel
            {
                Margin = new Thickness(6)
            };
            pan.Children.Add(photo);
            pan.Children.Add(new TextBlock { Text = "在照片上点选颜色", Margin = new Thickness(0, 4, 0, 3) });
            foreach (string s in new[]
            {
                "毛色",
                "眼睛",
                "花纹"
            }

            )
                samplePart.Items.Add(s);
            samplePart.SelectedIndex = 0;
            pan.Children.Add(samplePart);
            photo.MouseLeftButtonDown += delegate (object sender, System.Windows.Input.MouseButtonEventArgs args)
            {
                if (sourcePhoto == null)
                    return;
                Point pt = args.GetPosition(photo);
                double scale = Math.Min(photo.ActualWidth / sourcePhoto.PixelWidth, photo.ActualHeight / sourcePhoto.PixelHeight);
                if (scale <= 0)
                    return;
                double px = (pt.X - (photo.ActualWidth - sourcePhoto.PixelWidth * scale) / 2) / scale;
                double py = (pt.Y - (photo.ActualHeight - sourcePhoto.PixelHeight * scale) / 2) / scale;
                if (px >= 0 && px < sourcePhoto.PixelWidth && py >= 0 && py < sourcePhoto.PixelHeight)
                    SetColor(samplePart.SelectedIndex, PhotoPalette.Sample(sourcePhoto, (int)px, (int)py));
            };
            photoHolder.Background = Brushes.White;
            photoHolder.BorderBrush = Line;
            photoHolder.BorderThickness = new Thickness(1);
            photoHolder.Child = pan;
            photoHolder.Visibility = Visibility.Collapsed;
            controls.Children.Add(photoHolder);
        }

        private void OpenPhoto()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择猫咪照片",
                Filter = "照片|*.jpg;*.jpeg;*.png;*.bmp|所有文件|*.*"
            };
            if (dialog.ShowDialog(this) != true)
                return;
            try
            {
                var image = PhotoPalette.Read(dialog.FileName);
                string coat = PhotoPalette.EstimateFur(image);
                sourcePhoto = image;
                photo.Source = image;
                photoHolder.Visibility = Visibility.Visible;
                SetColor(0, coat);
                photoHolder.BringIntoView();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法读取这张照片。\n" + ex.Message, "我的猫", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BuildControls()
        {
            Heading("名字");
            var name = new TextBox
            {
                Text = draft.Name,
                MaxLength = 40,
                Padding = new Thickness(5)
            };
            name.TextChanged += delegate
            {
                draft.Name = name.Text;
            };
            controls.Children.Add(name);
            Heading("颜色");
            var swatches = new WrapPanel();
            controls.Children.Add(swatches);
            string[] descriptions =
            {
                "毛色",
                "眼睛",
                "花纹"
            };
            for (int i = 0; i < 3; i++)
            {
                int target = i;
                colors[i] = ButtonFor(descriptions[i], delegate
                {
                    ChooseColor(target);
                });
                swatches.Children.Add(colors[i]);
                RefreshColor(i);
            }

            Heading("花色");
            var styles = new ComboBox();
            string[] codes =
            {
                "solid",
                "tuxedo",
                "tabby",
                "calico"
            };
            foreach (string s in new[]
            {
                "纯色",
                "白胸口",
                "虎斑",
                "三花"
            }

            )
                styles.Items.Add(s);
            styles.SelectedIndex = Math.Max(0, Array.IndexOf(codes, draft.Pattern));
            styles.SelectionChanged += delegate
            {
                if (styles.SelectedIndex >= 0)
                    draft.Pattern = codes[styles.SelectedIndex];
                Refresh();
            };
            controls.Children.Add(styles);
            Heading("局部标记（按画面左右）");
            var parts = new WrapPanel();
            controls.Children.Add(parts);
            AddCheck(parts, "左脸斑", draft.LeftCheek, delegate (bool v)
            {
                draft.LeftCheek = v;
            });
            AddCheck(parts, "右脸斑", draft.RightCheek, delegate (bool v)
            {
                draft.RightCheek = v;
            });
            AddCheck(parts, "左白爪", draft.LeftSock, delegate (bool v)
            {
                draft.LeftSock = v;
            });
            AddCheck(parts, "右白爪", draft.RightSock, delegate (bool v)
            {
                draft.RightSock = v;
            });
            Heading("体型");
            var build = new Slider
            {
                Minimum = .8,
                Maximum = 1.25,
                Value = draft.Build,
                Margin = new Thickness(1, 3, 2, 8)
            };
            build.ValueChanged += delegate
            {
                draft.Build = build.Value;
                Refresh();
            };
            controls.Children.Add(build);
        }

        private void AddCheck(WrapPanel p, string text, bool initial, Action<bool> change)
        {
            var c = new CheckBox
            {
                Content = text,
                IsChecked = initial,
                MinWidth = 88,
                Margin = new Thickness(1, 3, 2, 3)
            };
            c.Click += delegate
            {
                change(c.IsChecked == true);
                Refresh();
            };
            p.Children.Add(c);
        }

        private string ColorAt(int i)
        {
            return i == 0 ? draft.Fur : i == 1 ? draft.Eyes : draft.Marking;
        }

        private void RefreshColor(int i)
        {
            Color c = (Color)ColorConverter.ConvertFromString(ColorAt(i));
            colors[i].Background = new SolidColorBrush(c);
            colors[i].Foreground = (c.R * .3 + c.G * .59 + c.B * .11) > 140 ? Ink : Brushes.White;
            colors[i].ToolTip = ColorAt(i);
        }

        private void SetColor(int i, string color)
        {
            if (i == 0)
                draft.Fur = color;
            else if (i == 1)
                draft.Eyes = color;
            else
                draft.Marking = color;
            RefreshColor(i);
            Refresh();
        }

        private void ChooseColor(int i)
        {
            using (var picker = new Forms.ColorDialog
            {
                FullOpen = true,
                Color = System.Drawing.ColorTranslator.FromHtml(ColorAt(i))
            }

            )
            {
                if (picker.ShowDialog() == Forms.DialogResult.OK)
                    SetColor(i, String.Format("#{0:X2}{1:X2}{2:X2}", picker.Color.R, picker.Color.G, picker.Color.B));
            }
        }

        private void Refresh()
        {
            preview.Look = draft;
            preview.InvalidateVisual();
        }

        private void DemoKeyPress()
        {
            char key = DemoKeys[sampleKey];
            sampleKey = (sampleKey + 1) % DemoKeys.Length;
            preview.PressKey(key, key.ToString());
        }

        private void SetFastTyping(bool enabled)
        {
            preview.ClearInput();
            double now = clock.Elapsed.TotalSeconds;
            fastTypingUntil = enabled ? now + 9 : 0;
            nextDemoPress = now;
            fastTypingButton.Content = enabled ? "停止快打" : "试试快打";
        }

        private void OnFrame(object sender, EventArgs args)
        {
            double now = clock.Elapsed.TotalSeconds;
            if (fastTypingUntil > 0)
            {
                if (now >= fastTypingUntil)
                    SetFastTyping(false);
                else if (now >= nextDemoPress)
                {
                    DemoKeyPress();
                    nextDemoPress = now + 0.21;
                }
            }
            Point p = System.Windows.Input.Mouse.GetPosition(preview);
            bool demo = moveUntil > now;
            double dx = demo ? Math.Sin(now * 6) : 2 * p.X / Math.Max(1, preview.ActualWidth) - 1;
            double dy = demo ? Math.Cos(now * 6) : 2 * p.Y / Math.Max(1, preview.ActualHeight) - 1;
            preview.SetPointer(dx, dy, false, p);
            preview.Tick(Math.Min(.1, now - previous));
            previous = now;
        }
    }
}
