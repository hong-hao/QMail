package ime.mail.mailbox;

import ime.mail.MailAccount;

import java.util.List;

import javax.mail.Session;

public interface IMailBox {
	
	long getId();

	String getName();
	
	void setMailSession(Session mailSession);
	Session getMailSession();
	
	MailAccount getMailAccount();
	
	List<IMailFolder> getFolders();
	
	IMailFolder getFolder(String name);
	
	IMailFolder createFolder(String name, long type) throws Exception;
	
	void removeFolder(String name) throws Exception;
}
