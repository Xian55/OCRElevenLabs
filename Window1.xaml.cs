using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.IO;
using Protonox.Labs.Api;
using static Protonox.Win32;

namespace Protonox
{
    public sealed partial class Window1 : Window
    {
        #region Variables

        private readonly WindowInteropHelper wih;

        private bool overlayStatus = true;

        private readonly int borderThickness;
        private double screenWidth = SystemParameters.PrimaryScreenWidth;
        private double screenHeight = SystemParameters.PrimaryScreenHeight;

        private System.Drawing.Point screenOffset;
        private System.Drawing.Rectangle selectionRect;

        private readonly Point minRect = new(5, 5);

        private bool dragging;
        private bool rectDrawn;
        private bool leftClickDown;

        private Point lastMousePoint;

        private HitType hitType = HitType.None;

        // Hotkeys
        private HwndSource hk_hWndSource;
        private const short hk_id_1 = 1;
        private const short hk_id_2 = 2;

        private string text;

        private readonly Controller controller;
        private readonly AudioSettings audioSettings;

        #endregion

        public Window1(Controller controller, AudioSettings audioSettings)
        {
            this.controller = controller;
            this.audioSettings = audioSettings;

            wih = new(this);

            LocationChanged += OnLocationChanged;
            PreviewKeyDown += new KeyEventHandler(HandleKeyDown_window);

            InitializeComponent();
            DataContext = this;

            borderThickness = (int)SelectionBorder.BorderThickness.Left;

            ResetSelectionRect();

            var dpd1 = DependencyPropertyDescriptor.FromProperty(Label.ContentProperty, typeof(Label));
            dpd1.AddValueChanged(outputText,
                (s, e) => TTSButton.IsEnabled = text is not null && audioSettings.Voice != null);

            var dpd2 = DependencyPropertyDescriptor.FromProperty(Slider.ValueProperty, typeof(Slider));
            dpd2.AddValueChanged(VolumeSlider,
                (s, e) => audioSettings.Volume = (float)VolumeSlider.Value / 100f);

            var dpd3 = DependencyPropertyDescriptor.FromProperty(ComboBox.SelectedValueProperty, typeof(ComboBox));
            dpd3.AddValueChanged(VoiceDropdown,
                (s, e) => audioSettings.Voice = VoiceDropdown.SelectedValue?.ToString());

            var dpd4 = DependencyPropertyDescriptor.FromProperty(Slider.ValueProperty, typeof(Slider));
            dpd4.AddValueChanged(SimilaritySlider,
                (s, e) => audioSettings.Similarity = (float)SimilaritySlider.Value / 100f);

            var dpd5 = DependencyPropertyDescriptor.FromProperty(Slider.ValueProperty, typeof(Slider));
            dpd5.AddValueChanged(StabilitySlider,
                (s, e) => audioSettings.Stability = (float)StabilitySlider.Value / 100f);

            audioSettings.Volume = (float)VolumeSlider.Value / 100f;
            audioSettings.Similarity = (float)SimilaritySlider.Value / 100f;
            audioSettings.Stability = (float)StabilitySlider.Value / 100f;

            TTSButton.IsEnabled = false;
            VoiceDropdown.IsEnabled = false;
            SimilaritySlider.IsEnabled = false;
            StabilitySlider.IsEnabled = false;

            VolumeSlider.IsEnabled = false;
        }

        private void OnLocationChanged(object sender, EventArgs e)
        {
            var screen = System.Windows.Forms.Screen.FromHandle(wih.Handle);
            screenOffset = new(screen.Bounds.Location.X, screen.Bounds.Location.Y);

            screenWidth = screen.Bounds.Width;
            screenHeight = screen.Bounds.Height;
        }

        public void ToggleOverlay(bool enabled)
        {
            if (enabled)
            {
                WindowState = WindowState.Maximized;
                Activate();
            }
            else
                WindowState = WindowState.Minimized;

            overlayStatus = enabled;
        }

        #region Selection Editor

        private enum HitType
        {
            None, Body, UL, UR, LR, LL, L, R, T, B
        };

        private HitType SetHitType(Point point)
        {
            double left = Canvas.GetLeft(SelectionBorder);
            double top = Canvas.GetTop(SelectionBorder);
            double right = left + SelectionBorder.Width;
            double bottom = top + SelectionBorder.Height;

            if (point.X < left) return HitType.None;
            if (point.X > right) return HitType.None;
            if (point.Y < top) return HitType.None;
            if (point.Y > bottom) return HitType.None;

            const double GAP = 10;
            if (point.X - left < GAP)
            {
                // Left edge.
                if (point.Y - top < GAP) return HitType.UL;
                if (bottom - point.Y < GAP) return HitType.LL;
                return HitType.L;
            }
            if (right - point.X < GAP)
            {
                // Right edge.
                if (point.Y - top < GAP) return HitType.UR;
                if (bottom - point.Y < GAP) return HitType.LR;
                return HitType.R;
            }
            if (point.Y - top < GAP) return HitType.T;
            if (bottom - point.Y < GAP) return HitType.B;
            return HitType.Body;
        }

        private void SetMouseCursor()
        {
            Cursor desired_cursor = Cursors.Arrow;
            switch (hitType)
            {
                case HitType.None:
                    desired_cursor = Cursors.Arrow;
                    break;
                case HitType.Body:
                    desired_cursor = Cursors.ScrollAll;
                    break;
                case HitType.UL:
                case HitType.LR:
                    desired_cursor = Cursors.SizeNWSE;
                    break;
                case HitType.LL:
                case HitType.UR:
                    desired_cursor = Cursors.SizeNESW;
                    break;
                case HitType.T:
                case HitType.B:
                    desired_cursor = Cursors.SizeNS;
                    break;
                case HitType.L:
                case HitType.R:
                    desired_cursor = Cursors.SizeWE;
                    break;
            }

            if (Cursor != desired_cursor)
                Cursor = desired_cursor;
        }

        private void ResetSelectionRect()
        {
            Canvas.SetLeft(SelectionBorder, 0);
            Canvas.SetTop(SelectionBorder, 0);

            SelectionBorder.Width = 0;
            SelectionBorder.Height = 0;

            selectionRect = new();

            Cursor = Cursors.Arrow;
        }

        private bool IsValidSelection()
        {
            return !selectionRect.IsEmpty;
        }

        #endregion

        #region Global Keybinding

        private void RegisterHotkeys()
        {
            hk_hWndSource = HwndSource.FromHwnd(wih.Handle);
            hk_hWndSource.AddHook(MainWindowProc);

            if (!RegisterHotKey(wih.Handle, hk_id_1, (uint)(ModifierKeys.Alt | ModifierKeys.Shift), VK_SPACE))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            if (!RegisterHotKey(wih.Handle, hk_id_2, (uint)ModifierKeys.Alt, VK_SPACE))
                throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        private void ReleaseHotkeys()
        {
            UnregisterHotKey(hk_hWndSource.Handle, hk_id_1);
            UnregisterHotKey(hk_hWndSource.Handle, hk_id_2);
            hk_hWndSource.RemoveHook(MainWindowProc);
        }

        private IntPtr MainWindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_HOTKEY:
                    //int vkey = (((int)lParam >> 16) & 0xFFFF);
                    ModifierKeys modifier = (ModifierKeys)((int)lParam & 0xFFFF);
                    if (modifier == (ModifierKeys.Alt | ModifierKeys.Shift))
                    {
                        ToggleOverlay(!overlayStatus);
                    }
                    else
                    {
                        Task.Run(QuickTTSPlayback);
                    }

                    handled = true;
                    break;
            }

            return IntPtr.Zero;
        }

        #endregion


        #region Tesseract Commands

        private void StartOCR()
        {
            int th = borderThickness;

            int x = selectionRect.X + screenOffset.X;
            int y = selectionRect.Y + screenOffset.Y;
            int w = selectionRect.Width;
            int h = selectionRect.Height;

            System.Drawing.Rectangle rect = new(x + th, y + th, w - 2 * th, h - 2 * th);

            text = controller.StartOCRThread(rect);

            UpdateOutputText(text.Trim());
        }

        private void UpdateOutputText(string text)
        {
            outputText.Dispatcher.Invoke(
                UpdateUIOutputText, text);
        }

        private void UpdateUIOutputText(string message)
        {
            outputText.Content = message;
        }

        #endregion

        #region Window Events

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            RegisterHotkeys();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ReleaseHotkeys();
        }

        private void HandleOnResize(object sender, SizeChangedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                overlayStatus = true;
            else
                overlayStatus = false;
        }

        #endregion


        #region Keyboard & Mouse Events

        private void HandleKeyDown_window(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
            }
            else if (e.Key == Key.D1)
            {
                outputText.SetValue(Grid.RowProperty, 0);
                outputText.SetValue(VerticalAlignmentProperty, VerticalAlignment.Top);
                outputText.SetValue(Grid.RowSpanProperty, 1);
            }
            else if (e.Key == Key.D2)
            {
                outputText.SetValue(Grid.RowProperty, 1);
                outputText.SetValue(VerticalAlignmentProperty, VerticalAlignment.Bottom);
                outputText.SetValue(Grid.RowSpanProperty, 1);
            }
            else if (e.Key == Key.D3)
            {
                outputText.SetValue(Grid.RowProperty, 2);
                outputText.SetValue(VerticalAlignmentProperty, VerticalAlignment.Bottom);
                outputText.SetValue(Grid.RowSpanProperty, 1);
            }
        }

        private void HandleMouseDown_canvas1(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                leftClickDown = true;

                if (IsValidSelection() && e.ClickCount == 2)
                {
                    StartOCR();
                    return;
                }

                hitType = SetHitType(Mouse.GetPosition(CanvasMain));
                SetMouseCursor();
                if (rectDrawn && hitType == HitType.None)
                {
                    ResetSelectionRect();
                    rectDrawn = false;
                }

                lastMousePoint = Mouse.GetPosition(CanvasMain);

                if (rectDrawn)
                    dragging = true;
                else
                {
                    //Set rect top left corner to cursor 
                    selectionRect.Location = new System.Drawing.Point
                    {
                        X = (int)lastMousePoint.X,
                        Y = (int)lastMousePoint.Y
                    };

                    Canvas.SetLeft(SelectionBorder, lastMousePoint.X);
                    Canvas.SetTop(SelectionBorder, lastMousePoint.Y);
                }
            }
            else if (e.ChangedButton == MouseButton.Right)
            {
                ToggleOverlay(false);
            }
        }

        private void HandleMouseUp_canvas1(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                dragging = false;
                leftClickDown = false;

                if (IsValidSelection())
                    rectDrawn = true;

                SetMouseCursor();
            }
        }

        private void HandleMouseMove_canvas1(object sender, MouseEventArgs e)
        {
            if (!leftClickDown)
            {
                hitType = SetHitType(Mouse.GetPosition(CanvasMain));
                SetMouseCursor();
                return;
            }

            if (!rectDrawn)
            {
                // See how much the mouse has moved.
                Point mousePoint = Mouse.GetPosition(CanvasMain);
                double deltaX = mousePoint.X - lastMousePoint.X;
                double deltaY = mousePoint.Y - lastMousePoint.Y;

                if (deltaX > minRect.X && deltaY > minRect.Y)
                {
                    SelectionBorder.Width = deltaX;
                    SelectionBorder.Height = deltaY;

                    selectionRect.Size = new((int)deltaX, (int)deltaY);
                }

                hitType = SetHitType(Mouse.GetPosition(CanvasMain));
                SetMouseCursor();

                return;
            }

            if (!dragging)
            {
                hitType = SetHitType(Mouse.GetPosition(CanvasMain));
                SetMouseCursor();
            }
            else
            {
                Point mousePoint = Mouse.GetPosition(CanvasMain);
                double deltaX = mousePoint.X - lastMousePoint.X;
                double deltaY = mousePoint.Y - lastMousePoint.Y;

                double newX = Canvas.GetLeft(SelectionBorder);
                double newY = Canvas.GetTop(SelectionBorder);
                double newWidth = SelectionBorder.Width;
                double newHeight = SelectionBorder.Height;

                if (newX <= 0)
                    newX = 0;

                if (newY <= 0)
                    newY = 0;

                if (newWidth > screenWidth)
                    newWidth = screenWidth;

                if (newHeight > screenHeight)
                    newHeight = screenHeight;

                if (newX + newWidth >= screenWidth)
                    newX -= newX + newWidth - screenWidth;

                if (newY + newHeight >= screenHeight)
                    newY -= newY + newHeight - screenHeight;

                switch (hitType)
                {
                    case HitType.Body:
                        newX += deltaX;
                        newY += deltaY;
                        break;
                    case HitType.UL:
                        newX += deltaX;
                        newY += deltaY;
                        newWidth -= deltaX;
                        newHeight -= deltaY;
                        break;
                    case HitType.UR:
                        newY += deltaY;
                        newWidth += deltaX;
                        newHeight -= deltaY;
                        break;
                    case HitType.LR:
                        newWidth += deltaX;
                        newHeight += deltaY;
                        break;
                    case HitType.LL:
                        newX += deltaX;
                        newWidth -= deltaX;
                        newHeight += deltaY;
                        break;
                    case HitType.L:
                        newX += deltaX;
                        newWidth -= deltaX;
                        break;
                    case HitType.R:
                        newWidth += deltaX;
                        break;
                    case HitType.B:
                        newHeight += deltaY;
                        break;
                    case HitType.T:
                        newY += deltaY;
                        newHeight -= deltaY;
                        break;
                }

                if ((newWidth > minRect.X) && (newHeight > minRect.Y))
                {
                    Canvas.SetLeft(SelectionBorder, newX);
                    Canvas.SetTop(SelectionBorder, newY);

                    SelectionBorder.Width = newWidth;
                    SelectionBorder.Height = newHeight;

                    this.selectionRect = new((int)newX, (int)newY, (int)newWidth, (int)newHeight);

                    lastMousePoint = mousePoint;
                }
            }
        }

        #endregion


        #region Button Click Events

        private void HandleOnClick_ButtonExit(object sender, RoutedEventArgs e)
        {
            Controller.Shutdown();
        }

        private void HandleOnClick_ButtonToggle(object sender, RoutedEventArgs e)
        {
            ToggleOverlay(!overlayStatus);
        }

        private async void HandleOnClick_ButtonInit(object sender, RoutedEventArgs e)
        {
            InitButton.Visibility = Visibility.Hidden;

            VoiceDropdown.Items.Clear();

            Voices[] voices = await Task.Run(controller.GetInitVoices);
            foreach (var item in voices)
            {
                VoiceDropdown.Items.Add(item.name);
            }

            if (VoiceDropdown.Items.Count > 0)
            {
                VoiceDropdown.SelectedIndex = 0;

                TTSButton.IsEnabled = text is not null;

                VoiceDropdown.IsEnabled = true;
                SimilaritySlider.IsEnabled = true;
                StabilitySlider.IsEnabled = true;

                VolumeSlider.IsEnabled = true;
            }

            InitButton.Visibility = Visibility.Visible;
        }

        private async void HandleOnClick_ButtonTTS(object sender, RoutedEventArgs e)
        {
            TTSButton.Visibility = Visibility.Hidden;

            string filePath = controller.AudioOutput(text);

            bool exists = File.Exists(filePath);
            if (exists || await PostTTS())
                controller.Playback(filePath);

            TTSButton.Visibility = Visibility.Visible;
        }

        #endregion

        private async Task<bool> PostTTS()
        {
            bool success = await controller.PostTTS(text);
            if (!success)
                UpdateOutputText("Error: Check Word Limit on EvenLabs!");

            return success;
        }

        private async Task QuickTTSPlayback()
        {
            if (!IsValidSelection() || audioSettings.Voice == null)
                return;

            StartOCR();

            string filePath = controller.AudioOutput(text);
            if (File.Exists(filePath))
            {
                controller.Playback(filePath);
                return;
            }

            if (await PostTTS())
                controller.Playback(filePath);
        }

    }
}
