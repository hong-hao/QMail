package ime.mail.mailbox;

import ime.mail.MailEntry;

import java.util.List;


public interface IMailFolder {
	List<MailEntry> getMails();
	
	long getId();

	String getName();
	void setName(String name);
	
	Object getField(String name);
	void setField(String name, Object value);
	
	IMailBox getMailBox();
	void setMailBox(IMailBox mailBox);
	
	IMailFolder getParent();
	void setParent(IMailFolder parent);
	
	List<IMailFolder> getChildren();
	
	void create(IMailFolder parent) throws Exception;
	void save() throws Exception;
	void remove() throws Exception;
}
