using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ime.mail.Worker
{
	public class MailAccount
	{
		public enum RECV_TYPE { POP3, IMAP };

		public string account;
		public string name;
		public string password;
		public RECV_TYPE recv_type;
		public string recv_server;
		public int recv_port;
		public bool recv_ssl = false;
		public string send_server;
		public int send_port;
		public bool send_ssl = false;
        public string pubId;
	}
}
