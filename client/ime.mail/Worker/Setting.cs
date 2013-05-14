using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using wos.collections;
using ime.mail.Net.Mail;
using ime.mail.Net.MIME;

namespace ime.mail.Worker
{
	public class Setting
	{
		private static string SPAMHeader = "X-ad-flag:YES";
		private static Map<string, string> spamHeader = null;

		public static bool IsSpamMail(Mail_Message mail)
		{
			if( spamHeader == null )
				LoadSpamHeader();
			MIME_h[] value;
			foreach(string header in spamHeader.Keys)
			{
				value = mail.Header[header];
				if( value != null && value.Length > 0 && value[0] != null )
				{
					if( String.Compare(spamHeader[header], value[0].ValueToString().Trim(), true) == 0 )
						return true;
				}
			}
			return false;
		}
		private static void LoadSpamHeader()
		{
			spamHeader = new Map<string, string>();
			String header = SPAMHeader;
			if( header != null )
			{
				string[] headers = header.Split(';');
				foreach(string h in headers )
				{
					string[] part = h.Split(':');
					if( part.Length == 2 ){
						spamHeader.Add(part[0].Trim(), part[1].Trim());
					}
				}
			}
		}
	}
}
