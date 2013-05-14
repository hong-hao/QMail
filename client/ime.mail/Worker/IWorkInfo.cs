using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;

namespace ime.mail.Worker
{
	public interface IWorkInfo
	{
		void SetProgress(int total, int progress);
		void SetItemProgress(int total, int progress);
		void SetInfo(string info);
		void SetStatus(bool isSucess, string info);
		void AddDetail(string detailInfo, Color color);
		void CloseWindow();
        bool IsNewMail { get; set; }
	}
}
