using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wos.rpc.core;
using wos.collections;

namespace ime.mail
{
	public class MailManager
	{
		/// <summary>
		/// 获取当前用户的邮件列表
		/// </summary>
		/// <param name="lastSyncTime">最后一下同步的时间</param>
		/// <returns></returns>
		public List<ASObject> getUserMails(DateTime lastSyncTime, int start, int size)
		{
			//TODO 获取当前登入用户自lastSyncTime以后更新的所有邮件列表
			//如果lastSyncTime的时间小于等于1970年，则返回所有邮件
			//调用服务端方法：QueryService.executeSQLQuery()
			return null;
		}

		/// <summary>
		/// 从客户端向服务端同步邮件信息
		/// </summary>
		/// <param name="mails">当前用户未同步的邮件信息</param>
		public void syncUserMails(List<Map<string, object>> mails)
		{
			//TODO 调用服务端方法MailManager.syncUserMails()
		}
	}
}
