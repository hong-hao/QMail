using System;
using System.IO;
using System.Data.SQLite;
using System.Data;
using wos.library;

namespace ime.mail.Worker
{
    public class DBWorker
    {
        private static string db_file = Desktop.instance.ApplicationPath + "/mail/mail.db";
        private static SQLiteConnection conn = null;

        //邮件类型
        public enum MailType
        {
            SendMail = 1, //发送邮件
            RecvMail = 2, //接收邮件
            DraftMail = 3, //草稿
            DeletedMail = 4, //已删除邮件
            SpamMail = 5, //垃圾邮件
            CleanedMail = 6, //已清除邮件
            OutboxMail = 7  //待发送邮件邮件
        }

        //数据库创建脚本
        private static string db_create_sql = @"
	                            CREATE TABLE IF NOT EXISTS [ML_Mail](
		                            [id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
		                            [uuid] NCHAR(100) NOT NULL,			/*邮件的唯一ID*/
		                            [owner_user_id] INTEGER NOT NULL,	/*邮件记录所属的用户(Principal.id)*/
		                            [message_id] NCHAR(100) NULL,		/*邮件消息ID*/
		                            [subject] NCHAR(500) NOT NULL,		/*主题*/
		                            [sender] NVARCHAR(100) NULL,		/*邮件发送者*/
		                            [mail_from] NVARCHAR(300) NULL,		/*来源地址*/
		                            [mail_to] NVARCHAR(500) NULL,		/*目标地址*/
		                            [contact_mail] NVARCHAR(100) NULL,	/*联系人邮件地址*/
		                            [customer_id] INTEGER NULL,			/*客户ID*/
		                            [customer_label] NVARCHAR(200) NULL,/*客户名称*/
		                            [customer_grade] INTEGER NULL,		/*客户等级*/
		                            [mail_account] NVARCHAR(100) NULL,	/*邮件所属的帐号*/
		                            [reply_to] NVARCHAR(300) NULL,		/*回复地址*/
		                            [flags] NVARCHAR(100) NULL,			/*邮件标识*/
		                            [cc] NVARCHAR(300) NULL,			/*抄送地址*/
		                            [bcc] NVARCHAR(100) NULL,			/*密送地址*/
		                            [attachments] NVARCHAR(1000) NULL,	/*附件文件*/
		                            [has_attachments] smallint NULL,	/*是否有附件*/
		                            [contents] NTEXT NULL,				/*邮件内容描述(xml)*/
		                            [text_body] NTEXT NULL,				/*邮件的文本内容*/
		                            [html_body] NTEXT NULL,				/*邮件的html内容*/
		                            [create_time] TIMESTAMP NOT NULL,	/*邮件创建时间*/
		                            [mail_date] TIMESTAMP NULL,			/*邮件中的时间*/
		                            [send_time] TIMESTAMP NULL,			/*邮件发送时间*/
		                            [mail_type] INTERGER NULL,			/*邮件类型*/
		                            [mail_file] NVARCHAR(200) NULL,		/*邮件文件路径*/
		                            [reply_for] NCHAR(100) NULL,		/*所回复邮件的UUID*/
		                            [reply_header] NCHAR(100) NULL,		/*所回复邮件会话的源头邮件UUID*/
		                            [folder] NVARCHAR(100) NULL,		/*邮件所属的帐号目录*/
		                            [mail_uid] NCHAR(150) NULL,			/*邮件在邮件服务器中的UID*/
		                            [client_or_server] NCHAR(10) NULL,	/*是客户端接收的邮件还是服务端接收的邮件*/
		                            [is_synced] smallint NULL,			/*是否已上传或下载,0:未处理,1:记录已同步,2:邮件(eml文件)已同步*/
		                            [is_seen] smallint NULL,			/*是否已查看*/
                                    [is_handled] smallint NULL,			/*是否已处理*/
		                            [handle_action] INTEGER NULL,		/*处理方式*/
		                            [reviewer_id] INTEGER NULL,			/*审核人员ID*/
		                            [reviewer_name] NCHAR(50) NULL,		/*审核人员姓名*/
		                            [operator_id] INTEGER NULL,			/*处理人员ID*/
		                            [operator_name] NCHAR(50) NULL,		/*处理人员姓名*/
		                            [is_new_contact] smallint NULL,		/*是否新联系人*/
		                            [mail_from_label] NVARCHAR(100) NULL,/*来源地址的显示名称*/
		                            [mail_to_label] NVARCHAR(100) NULL,	/*目标地址的显示名称*/
		                            [ip_from] NVARCHAR(100) NULL,		/*邮件来源的IP地址*/
		                            [country_from] NCHAR(20) NULL,		/*邮件来源的国家简称*/
		                            [remark] NVARCHAR(500) NULL,		/*备注*/
		                            [priority] NVARCHAR(100) NULL,		/*优先级*/
                                    [area_from] NVARCHAR(500) NULL		/*来源地区*/
	                            );

                                CREATE TABLE IF NOT EXISTS [ML_Mail_Temp](
		                            [id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
		                            [uuid] NCHAR(100) NOT NULL,			/*邮件的唯一ID*/
		                            [owner_user_id] INTEGER NOT NULL,	/*邮件记录所属的用户(Principal.id)*/
		                            [message_id] NCHAR(100) NULL,		/*邮件消息ID*/
		                            [subject] NCHAR(500) NOT NULL,		/*主题*/
		                            [sender] NVARCHAR(100) NULL,		/*邮件发送者*/
		                            [mail_from] NVARCHAR(300) NULL,		/*来源地址*/
		                            [mail_to] NVARCHAR(500) NULL,		/*目标地址*/
		                            [contact_mail] NVARCHAR(100) NULL,	/*联系人邮件地址*/
		                            [customer_id] INTEGER NULL,			/*客户ID*/
		                            [customer_label] NVARCHAR(200) NULL,/*客户名称*/
		                            [customer_grade] INTEGER NULL,		/*客户等级*/
		                            [mail_account] NVARCHAR(100) NULL,	/*邮件所属的帐号*/
		                            [reply_to] NVARCHAR(300) NULL,		/*回复地址*/
		                            [flags] NVARCHAR(100) NULL,			/*邮件标识*/
		                            [cc] NVARCHAR(300) NULL,			/*抄送地址*/
		                            [bcc] NVARCHAR(100) NULL,			/*密送地址*/
		                            [attachments] NVARCHAR(1000) NULL,	/*附件文件*/
		                            [has_attachments] smallint NULL,	/*是否有附件*/
		                            [contents] NTEXT NULL,				/*邮件内容描述(xml)*/
		                            [text_body] NTEXT NULL,				/*邮件的文本内容*/
		                            [html_body] NTEXT NULL,				/*邮件的html内容*/
		                            [create_time] TIMESTAMP NOT NULL,	/*邮件创建时间*/
		                            [mail_date] TIMESTAMP NULL,			/*邮件中的时间*/
		                            [send_time] TIMESTAMP NULL,			/*邮件发送时间*/
		                            [mail_type] INTERGER NULL,			/*邮件类型*/
		                            [mail_file] NVARCHAR(200) NULL,		/*邮件文件路径*/
		                            [reply_for] NCHAR(100) NULL,		/*所回复邮件的UUID*/
		                            [reply_header] NCHAR(100) NULL,		/*所回复邮件会话的源头邮件UUID*/
		                            [folder] NVARCHAR(100) NULL,		/*邮件所属的帐号目录*/
		                            [mail_uid] NCHAR(150) NULL,			/*邮件在邮件服务器中的UID*/
		                            [client_or_server] NCHAR(10) NULL,	/*是客户端接收的邮件还是服务端接收的邮件*/
		                            [is_synced] smallint NULL,			/*是否已上传或下载,0:未处理,1:记录已同步,2:邮件(eml文件)已同步*/
		                            [is_seen] smallint NULL,			/*是否已查看*/
                                    [is_handled] smallint NULL,			/*是否已处理*/
		                            [handle_action] INTEGER NULL,		/*处理方式*/
		                            [reviewer_id] INTEGER NULL,			/*审核人员ID*/
		                            [reviewer_name] NCHAR(50) NULL,		/*审核人员姓名*/
		                            [operator_id] INTEGER NULL,			/*处理人员ID*/
		                            [operator_name] NCHAR(50) NULL,		/*处理人员姓名*/
		                            [is_new_contact] smallint NULL,		/*是否新联系人*/
		                            [mail_from_label] NVARCHAR(100) NULL,/*来源地址的显示名称*/
		                            [mail_to_label] NVARCHAR(100) NULL,	/*目标地址的显示名称*/
		                            [ip_from] NVARCHAR(100) NULL,		/*邮件来源的IP地址*/
		                            [country_from] NCHAR(20) NULL,		/*邮件来源的国家简称*/
		                            [remark] NVARCHAR(500) NULL,		/*备注*/
		                            [priority] NVARCHAR(100) NULL,		/*优先级*/
                                    [area_from] NVARCHAR(500) NULL		/*来源地区*/
	                            );
                            ";
        public static bool IsDBCreated()
        {
            return File.Exists(db_file);
        }
        public static void CreateDB()
        {
            DirectoryInfo dir = Directory.GetParent(db_file);
            if (!dir.Exists)
                dir.Create();

            SQLiteConnection.CreateFile(db_file);
            SQLiteCommand cmd = null;
            try
            {
                cmd = GetConnection().CreateCommand();
                cmd.CommandText = db_create_sql;

                cmd.ExecuteNonQuery();
                cmd.Dispose();
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
        }
        public static DataSet ExecuteQuery(string query)
        {
            DataSet ds = new DataSet();
            try
            {
                using (SQLiteCommand cmd = new SQLiteCommand(query, GetConnection()))
                {
                    using (SQLiteDataAdapter q = new SQLiteDataAdapter(cmd))
                    {
                        q.Fill(ds);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.Write(ex.StackTrace);
            }

            return ds;
        }
        public static SQLiteConnection GetConnection()
        {
            if (conn == null)
            {
                conn = new SQLiteConnection("Data Source=" + db_file);
                if(conn.State == ConnectionState.Closed)
                    conn.Open();
            }

            return conn;
        }

        public static void CloseConnection()
        {
            if (conn == null)
                return;
            conn.Close();
            conn.Dispose();
            conn = null;
        }
    }
}