using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using ime.mail.Worker;
using ime.notification;
using wos.extensions;
using wos.library;
using wos.plugin;
using wos.rpc.core;
using System.Timers;
using System.Windows;

namespace ime.mail
{
    public class Plugin : IPlugin
    {
        #region IPlugin 成员
        private string MailDsBoxDeliver = "MailDsBoxDeliver";
        private string MailMoveDeliver = "MailMoveDeliver";
        private string MailAuditedDeliver = "MailAuditedDeliver";

        public void load(Dictionary<string, string> properties = null)
        {
            Desktop.registerALinkHandler("ime.mail", new ALinkHandler());

			ApplicationIcon app = new ApplicationIcon();
			app.Text = "邮件管理";
			app.ALink = "alink://ime.mail/MailManager";
			app.ImageUrl = "pack://application:,,,/ime.mail;component/icon.png";

			PluginManager.ApplicationsIcons.Add(app);

            NotificationCenter.Instance.AddMessageListener(MailDsBoxDeliver, onMailDsBoxDeliver);
            NotificationCenter.Instance.AddMessageListener(MailMoveDeliver, onMailDsBoxDeliver);
            NotificationCenter.Instance.AddMessageListener(MailAuditedDeliver, onMailAudited);

            Desktop.instance.DesktopEvent += onDesktopEvent;
        }

        void onDesktopEvent(string eventName, string action, object args)
        {
            if (eventName == "user")
            {
                if (action == "logout")
                {
                    NotificationCenter.Instance.RemoveMessageListener(MailDsBoxDeliver, onMailDsBoxDeliver);
                    NotificationCenter.Instance.RemoveMessageListener(MailMoveDeliver, onMailDsBoxDeliver);
                    NotificationCenter.Instance.RemoveMessageListener(MailAuditedDeliver, onMailAudited);
                    Desktop.instance.DesktopEvent -= onDesktopEvent;
                }
            }
        }

        #endregion

        private void onMailDsBoxDeliver(string sender, ime.notification.NotifyMessage e, NotificationCenter.Stage stage)
        {
            if (stage == NotificationCenter.Stage.Receiving)
            {
                Application.Current.Dispatcher.Invoke((System.Action)delegate
                {
                    e.Show();
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
            else if (stage == NotificationCenter.Stage.UserInteracting)
            {
                Application.Current.Dispatcher.Invoke((System.Action)delegate
                {
                    Desktop.openALinkWindow(ALink.parseALink("alink://ime.mail/MailManager"));
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }

        private void onMailAudited(string sender, ime.notification.NotifyMessage e, NotificationCenter.Stage stage)
        {
            if (stage == NotificationCenter.Stage.Receiving)
            {
                Application.Current.Dispatcher.Invoke((System.Action)delegate
                {
                    if (!DBWorker.IsDBCreated())
                        return;
                    XElement xml = e.Body as XElement;
                    if (xml == null)
                        return;
                    XElement msgXml = xml.Element("message");
                    if (msgXml == null)
                        return;

                    ASObject mail = new ASObject();
                    mail["uuid"] = msgXml.AttributeValue("uuid");
                    mail["folder"] = "SENDED";
                    MailWorker.instance.updateMailRecord(mail, new string[] { "folder" });
                    MailWorker.instance.dispatchMailEvent(MailWorker.Event.Reset, null, null);
                    Application.Current.Dispatcher.Invoke((System.Action)delegate
                    {
                        e.Show();
                    }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            }
        }
    }
}
