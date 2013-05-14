using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using ime.data.Grouping;
using ime.mail.Worker;
using wos.library;
using wos.rpc.core;

namespace ime.mail.controls
{
    /// <summary>
    /// 待审邮件
    /// </summary>
    public class DsBoxMailProvider : IGroupDataProvider<ASObject>
    {
        class DsBoxPhase
        {
            public string name;		//分段名称
            public int type;
            public Collection<ASObject> data = new Collection<ASObject>(); //符合时间范围的数据列表
        }

        private List<DsBoxPhase> dsBoxPhases; //分组

        private void InitDsBoxPhases()
        {
            dsBoxPhases = new List<DsBoxPhase>();

            DsBoxPhase phase = new DsBoxPhase();
            phase.name = "提交审核的邮件";
            phase.type = 1;
            dsBoxPhases.Add(phase);

            phase = new DsBoxPhase();
            phase.name = "等待审核的邮件";
            phase.type = 2;
            dsBoxPhases.Add(phase);
        }

        public GroupCollection<ASObject> GetMails(string folder)
        {
            InitDsBoxPhases();
            GroupCollection<ASObject> result = new GroupCollection<ASObject>(this);

            ASObjectGroup group = null;

            foreach (DsBoxPhase phase in dsBoxPhases)
            {
                phase.data = FetchMail("DSBOX", phase.type);
                group = new ASObjectGroup(result, true);
                group.Children = phase.data;
                group.ChildrenCount = phase.data.Count;
                group["$type"] = phase.type;
                group["$group_label"] = phase.name;
                group["$children_count"] = "(" + group.ChildrenCount + ")";
                group["$mail_folder"] = folder;

                result.Add(group);
                result.InsertRange(result.Count, phase.data);

            }

            return result;
        }

        private Collection<ASObject> FetchMail(string folder, int type)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("select * from ML_Mail where folder = '").Append(folder).Append("' ");
            if(type == 1)
                sql.Append("and reviewer_id != @reviewer_id and owner_user_id=@owner_user_id");
            else
                sql.Append("and reviewer_id = @reviewer_id and owner_user_id=@owner_user_id");

            SQLiteCommand cmd = null;
            DataSet ds = null;
            try
            {
                cmd = new SQLiteCommand(sql.ToString(), DBWorker.GetConnection());
                cmd.Parameters.AddWithValue("@owner_user_id", Desktop.instance.loginedPrincipal.id);
                cmd.Parameters.AddWithValue("@reviewer_id", Desktop.instance.loginedPrincipal.id);
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

        public Collection<ASObject> GetChildren(ASObject group)
        {
            string folder = group.getString("$mail_folder");
            int type = group.getInt("type");
            Collection<ASObject> children = FetchMail(folder, type);
            group["$children_count"] = "(" + children.Count + ")";

            return children;
        }


        public void AddToGroup(GroupCollection<ASObject> collection, ASObject obj)
        {
            IEnumerable<ASObjectGroup> groups = collection.OfType<ASObjectGroup>();
            int index;
            foreach (ASObjectGroup group in groups)
            {
                if (group.getString("$type") == "1")
                {
                    if (group.IsExpanded)
                    {
                        index = collection.IndexOf(group);
                        collection.Insert(index + group.ChildrenCount, obj);
                    }
                    group.Children.Add(obj);
                    group.ChildrenCount += 1;
                    break;
                }
            }
        }
    }
}