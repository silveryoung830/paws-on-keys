using System;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PawsOnKeys
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            using (var mutex = new Mutex(false, @"Local\PawsOnKeys.Main"))
            {
                bool locked;
                try { locked = mutex.WaitOne(0); }
                catch (AbandonedMutexException) { locked = true; }
                if (!locked)
                {
                    try { using (var reopen = EventWaitHandle.OpenExisting(@"Local\PawsOnKeys.Edit")) reopen.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return 0;
                }
                try { return Run(); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "桌角猫 · 启动失败", MessageBoxButton.OK, MessageBoxImage.Error); return 1; }
                finally { mutex.ReleaseMutex(); }
            }
        }

        private static int Run()
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var settings = PetSettings.Load(PetSettings.DefaultPath);
            if (!settings.Configured)
            {
                var editor = new CatEditor(settings.Cat);
                if (editor.ShowDialog() != true) return 0;
                settings.Cat = editor.Result;
                settings.Cat.Normalize();
                settings.Configured = true;
                try { settings.Save(PetSettings.DefaultPath); }
                catch (IOException ex) { MessageBox.Show(ex.Message, "设置仅在本次运行生效"); }
                catch (UnauthorizedAccessException ex) { MessageBox.Show(ex.Message, "设置仅在本次运行生效"); }
            }

            var window = new PetWindow(settings);
            app.MainWindow = window;
            app.ShutdownMode = ShutdownMode.OnMainWindowClose;
            using (var editSignal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\PawsOnKeys.Edit"))
            {
                var wait = ThreadPool.RegisterWaitForSingleObject(editSignal, delegate
                {
                    if (!app.Dispatcher.HasShutdownStarted) app.Dispatcher.BeginInvoke(new Action(window.OpenEditor));
                }, null, Timeout.Infinite, false);
                try
                {
                    window.Show();
                    window.ShowTip("桌角猫已就位", "双击托盘猫图标编辑形象，右键可暂停、移动或退出。");
                    return app.Run();
                }
                finally { wait.Unregister(null); }
            }
        }
    }
}