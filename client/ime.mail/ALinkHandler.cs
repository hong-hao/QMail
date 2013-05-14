using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wos.library;
using System.Windows;

namespace ime.mail
{
    public class ALinkHandler : IALinkHandler
    {
        public void handleALink(ALink alink)
        {
            try
            {
                if (alink.form == "MailManager")
                {
					Window window = Desktop.instance.getApplicationWindow("ime.MailManager");
					if (window != null)
					{
						if (window.WindowState == System.Windows.WindowState.Minimized)
							window.WindowState = System.Windows.WindowState.Normal;
						window.Activate();
						return;
					}
                    MailWindow win = new MailWindow();
                    win.Owner = Application.Current.MainWindow;
                    win.Show();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }
        }
    }
}
