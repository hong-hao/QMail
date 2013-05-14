using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections.ObjectModel;
using ime.mail.Worker;
using System.Data.SQLite;
using wos.rpc.core;
using System.Data;
using wos.library;
using wos.utils;
using wos.collections;

namespace ime.mail.controls
{
    /// <summary>
    /// 未处理邮件
    /// </summary>
    public class UnhandledMailProvider
    {
        class TimePhase
        {
            public string name;		//分段名称
            public DateTime begin;	//起始时间
            public DateTime end;	//结束时间
        }

        List<TimePhase> timePhases = null;

        public UnhandledMailProvider()
        {
            InitTimePhases();
        }

        /// <summary>
		/// 初始化时间分段
		/// </summary>
        private List<TimePhase> InitTimePhases()
        {
            DateTime now = DateTimeUtil.now();
            DateTime today = new DateTime(now.Year, now.Month, now.Day);

            timePhases = new List<TimePhase>();
            TimePhase phase;

            //两天
            phase = new TimePhase();
            phase.name = "0";
            phase.begin = today.AddDays(-2);
            phase.end = now;
            timePhases.Add(phase);

            //四天
            phase = new TimePhase();
            phase.name = "1";
            phase.begin = today.AddDays(-4);
            phase.end = today.AddDays(-2);
            timePhases.Add(phase);

            //一周
            phase = new TimePhase();
            phase.name = "2";
            phase.begin = today.AddDays(-7);
            phase.end = today.AddDays(-4);
            timePhases.Add(phase);

            //半个月
            //先获取一个月的时间差 然后除以2加一就是半个月的时间差
            int days = today.Subtract(today.AddMonths(-1)).Days;
            phase = new TimePhase();
            phase.name = "3";
            phase.begin = today.AddDays(-(days / 2));
            phase.end = today.AddDays(-7);
            timePhases.Add(phase);

            //一个月
            phase = new TimePhase();
            phase.name = "4";
            phase.begin = today.AddMonths(-1);
            phase.end = today.AddDays(-(days / 2));
            timePhases.Add(phase);

            //三个月
            phase = new TimePhase();
            phase.name = "5";
            phase.begin = today.AddMonths(-3);
            phase.end = today.AddMonths(-1);
            timePhases.Add(phase);

            //更长时间
            phase = new TimePhase();
            phase.name = "6";
            phase.begin = DateTime.MinValue;
            phase.end = today.AddMonths(-3);
            timePhases.Add(phase);

            return timePhases;
        }

        private TimePhase FindTimePhase(string value)
        {
            foreach (TimePhase TimePhase in timePhases)
            {
                if (value == TimePhase.name)
                    return TimePhase;
            }

            return null;
        }

        /// <summary>
        /// 判断时间是属于哪段的
        /// </summary>
        /// <param name="date"></param>
        /// <returns>0：两天，1：四天，2：一周，3：半个月，4:1个月，5:3个月，6：更长时间</returns>
        public string JudgeTimePhase(DateTime date)
        {
            foreach (TimePhase TimePhase in timePhases)
            {
                if (TimePhase.begin <= date && date <= TimePhase.end)
                    return TimePhase.name;
            }

            return null;
        }

        /// <summary>
        /// 获取未处理的邮件
        /// </summary>
        /// <param name="value">0：两天，1：四天，2：一周，3：半个月，4:1个月，5:3个月，6：更长时间</param>
        /// <param name="field">排序字段</param>
        /// <param name="sort">排序 asc desc</param>
        /// <returns></returns>
        public Collection<ASObject> GetMails(string value, string field = null, string sort = null)
        {
            TimePhase phase = FindTimePhase(value);
            if (phase == null)
                return null;
            return FetchMail(phase.begin, phase.end, field, sort);
        }

        private Collection<ASObject> FetchMail(DateTime begin, DateTime end, string field, string sort)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = 'INBOX' ")
               .Append("and owner_user_id = @owner_user_id ");
            if (begin != DateTime.MinValue)
                sql.Append("and mail_date > @begin_time ");
            
            if (end != DateTime.MinValue)
                sql.Append("and mail_date <= @end_time ");

            sql.Append("and (is_handled is null or is_handled = 0) ");

            if (String.IsNullOrWhiteSpace(field))
                sql.Append(" order by mail_date desc");
            else
                sql.Append(" order by ").Append(field).Append(" ").Append(sort);

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                if (begin != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@begin_time", begin);
                if (end != DateTime.MinValue)
                    cmd.Parameters.AddWithValue("@end_time", end);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            Collection<ASObject> data = new Collection<ASObject>();
            DataTable dt = ds.Tables[0];

            object value;
            foreach (DataRow row in dt.Rows)
            {
                ASObject mail = new ASObject();
                foreach (DataColumn column in dt.Columns)
                {
                    value = row[column];
                    if (value is System.DBNull)
                        mail[column.ColumnName] = null;
                    else
                        mail[column.ColumnName] = value;
                }
                data.Add(mail);
            }
            return data;
        }

        public Map<string, long> FetchUnhandledMailCount()
        {
            Map<string, long> count = new Map<string, long>();
            StringBuilder sql = new StringBuilder();
            sql.Append("select mail_date from ML_Mail where folder = 'INBOX' ")
               .Append("and owner_user_id = @owner_user_id ");

            sql.Append("and (is_handled is null or is_handled = 0) ");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);

                ds = new DataSet();
                SQLiteDataAdapter q = new SQLiteDataAdapter(cmd);
                q.Fill(ds);
            }
            finally
            {
                if (cmd != null)
                    cmd.Dispose();
            }
            
            DataTable dt = ds.Tables[0];
            foreach (DataRow row in dt.Rows)
            {
                if (row[0] is System.DBNull)
                    continue;
                DateTime date = (DateTime)row[0];
                foreach (TimePhase phase in timePhases)
                {
                    if (date >= phase.begin && date < phase.end)
                    {
                        if (!count.ContainsKey(phase.name))
                            count.Add(phase.name, 1);
                        else
                        {
                            long index = count[phase.name];
                            count.Add(phase.name, index + 1);
                        }
                    }
                }
            }
            return count;
        }
    }
}
