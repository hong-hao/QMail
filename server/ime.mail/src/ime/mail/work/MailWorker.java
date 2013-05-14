package ime.mail.work;

import ime.mail.MailAccount;
import ime.mail.MailEntry;
import ime.mail.SpamManager;
import ime.mail.mailbox.IMailFolder;
import ime.mail.mailbox.MailBox;
import ime.mail.mailbox.MailFolder;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Properties;
import java.util.Set;

import javax.mail.AuthenticationFailedException;
import javax.mail.Authenticator;
import javax.mail.FetchProfile;
import javax.mail.Folder;
import javax.mail.Message;
import javax.mail.MessagingException;
import javax.mail.PasswordAuthentication;
import javax.mail.Session;
import javax.mail.Store;
import javax.mail.Transport;
import javax.mail.UIDFolder;
import javax.mail.internet.MimeMessage;

import org.directwebremoting.util.Logger;
import org.hibernate.Transaction;

import com.sun.mail.imap.IMAPFolder;
import com.sun.mail.pop3.POP3Folder;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

public class MailWorker implements Runnable {
	private static Logger logger = Logger.getLogger(MailWorker.class);

	private static final int BATCH_SIZE = 20;
	
	public interface IMessageListener{
		public void onMessage(String message);
	}
	public class PopupAuthenticator extends Authenticator {
		String username = null;
		String password = null;

		public PopupAuthenticator(String user, String pass) {
			username = user;
			password = pass;
		}

		protected PasswordAuthentication getPasswordAuthentication() {
			return new PasswordAuthentication(username, password);
		}
	}
	public enum State{
		Waiting,		//�ȴ���ִ��
		Runing,			//����ִ��
		Compledted,		//��ִ�����
		Stopping,		//���ڱ���ֹ
		Stoped			//�ѱ���ֹ
	}
	private List<IMessageListener> listeners;
	
	private State state;
	private MailAccount mailAccount;
	private MailBox mailBox;
	
	public MailWorker(MailBox mailBox){
		this.mailBox = mailBox;
		this.mailAccount = mailBox.getMailAccount();
	}
	
	@Override
	public void run() {
		try{
			sendMail();
			
			if( mailAccount.getRecvType() == MailAccount.POP3 )
				pop3RecvMail();
			else if( mailAccount.getRecvType() == MailAccount.IMAP )
				imapRecvMail();
		}
		catch(Exception e){
			logger.error(e.getMessage(), e);
		}
	}
	
	private void saveMail(List<MailEntry> mails){
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();
    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				mailBox.save();
				
				Set<String> uids = new HashSet<String>();
				if( mails != null ){
					for(MailEntry mail : mails ){
						mail.save();
						uids.add(mail.getUid());
					}
				}
				mailBox.saveUids(uids);
				
				if( owner_tx )
					tx.commit();
			} catch (Exception e) {
				if( owner_tx )
					tx.rollback();
				throw e;
			}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
			e.printStackTrace();
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
	}
	private void syncPop3MailBoxFolder(){
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();
    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				if( mailBox.getFolder(MailBox.INBOX) == null ){
					mailBox.createFolder(MailBox.INBOX, MailFolder.SERVER_FOLDER);
				}
				if( mailBox.getFolder(MailBox.OUTBOX) == null ){
					mailBox.createFolder(MailBox.OUTBOX, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.SENDED) == null ){
					mailBox.createFolder(MailBox.SENDED, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.DRAFT) == null ){
					mailBox.createFolder(MailBox.DRAFT, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.SPAM) == null ){
					mailBox.createFolder(MailBox.SPAM, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.DELETED) == null ){
					mailBox.createFolder(MailBox.DELETED, MailFolder.SYSTEM_FOLDER);
				}
				mailBox.setStatus(MailBox.CREATED_STATUS);
				
				if( owner_tx )
					tx.commit();
			} catch (Exception e) {
				if( owner_tx )
					tx.rollback();
				throw e;
			}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
	}
	private void syncImapMailBoxFolder(Store store) throws MessagingException{
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();
    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				if( mailBox.getFolder(MailBox.INBOX) == null ){
					mailBox.createFolder(MailBox.INBOX, MailFolder.SERVER_FOLDER);
				}
				if( mailBox.getFolder(MailBox.OUTBOX) == null ){
					mailBox.createFolder(MailBox.OUTBOX, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.SENDED) == null ){
					mailBox.createFolder(MailBox.SENDED, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.DRAFT) == null ){
					mailBox.createFolder(MailBox.DRAFT, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.OUTBOX) == null ){
					mailBox.createFolder(MailBox.OUTBOX, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.SPAM) == null ){
					mailBox.createFolder(MailBox.SPAM, MailFolder.SYSTEM_FOLDER);
				}
				if( mailBox.getFolder(MailBox.DELETED) == null ){
					mailBox.createFolder(MailBox.DELETED, MailFolder.SYSTEM_FOLDER);
				}
				mailBox.setStatus(MailBox.CREATED_STATUS);
				
				if( owner_tx )
					tx.commit();
			} catch (Exception e) {
				if( owner_tx )
					tx.rollback();
				throw e;
			}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
	}
	/**
	 * ��IMAP��ʽ�����ʼ��������е��ʼ�
	 */
	private void imapRecvMail(){
		Store store = null;
		IMAPFolder folder = null;
		try{
			//��ȡĬ�ϻỰ
			Properties props = new Properties();
			props.put("mail.imaps.partialfetch", false);
			Session session = Session.getDefaultInstance(props, null);
			if( mailAccount.isRecvSSL() )
				store = session.getStore("imaps");
			else
				store = session.getStore("imap");
			
			try{
				store.connect(mailAccount.getRecvAddress(), mailAccount.getRecvPort(), mailAccount.getAccount(), mailAccount.getPassword());
			}catch( AuthenticationFailedException ex){
				mailBox.setLastError("�ʼ�����ʧ�ܣ������ʻ����������");
			}catch(Exception e){
				mailBox.setLastError(e.getMessage());
				throw e;
			}
			
			if( mailBox.getStatus() != MailBox.CREATED_STATUS )
				this.syncImapMailBoxFolder(store);
			
			IMailFolder mailFolder = mailBox.getFolder(MailBox.INBOX);
			
			//��ȡĬ���ļ���
			folder = (IMAPFolder)store.getFolder(MailBox.INBOX);
			if (folder == null) {
				String message = mailAccount.getAccount() + "�������ô����޷��ҵ�INBOXĿ¼��";
				logger.error(message);
				throw new Exception(message);
			}
			
			List<MailEntry> recvedMail = imapRecvMail(folder, mailFolder);
			saveMail(recvedMail);
		}catch (Exception ex){
			mailBox.setLastError("�ʼ�����ʧ�ܣ�" + ex.getMessage());
			logger.error(ex.getMessage(), ex);
		}
		finally{
			//�ͷ���Դ
			try{
				if (folder != null)
					folder.close(false);
				if (store != null)
					store.close();
			}
			catch (Exception e){
			}
		}
	}
	private List<MailEntry> imapRecvMail(IMAPFolder folder, IMailFolder mailFolder){
		List<MailEntry> recvedMail = new ArrayList<MailEntry>();
		try{
			IMailFolder spamFolder = mailBox.getFolder(MailBox.SPAM);
			
			//ʹ��ֻ����ʽ���ռ���
			folder.open(Folder.READ_ONLY);
			
			FetchProfile profile = new FetchProfile();
			profile.add(UIDFolder.FetchProfileItem.UID);
			Message[] messages = folder.getMessages();
			folder.fetch(messages, profile);
			Message message;
			int count = 0;
			for(int i = 0; i < messages.length; i++)
			{
				message = messages[i];
				String uid = Long.valueOf(folder.getUID(message)).toString();
				
				if(mailBox.isNewMail(uid) && message instanceof MimeMessage)
				{
					MailEntry mailEntry = new MailEntry(mailBox);
					mailEntry.setMailMessage((MimeMessage)message, uid);
					mailEntry.getEntity().put("mail_account_id", mailAccount.getEntity().get("id"));
					mailEntry.getEntity().put("mail_account_label", mailAccount.getAccount());
					mailEntry.setMailType(MailEntry.RECV_MAIL);
					mailEntry.addFlag(MailEntry.Flag.RECENT);
					if( SpamManager.isSpamMail(message) )
						mailEntry.setMailFolder(spamFolder);
					else
						mailEntry.setMailFolder(mailFolder);
					
					recvedMail.add(mailEntry);
					count ++;
					if( count > BATCH_SIZE ){
						saveMail(recvedMail);
						count = 0;
						recvedMail.clear();
					}
				}
			}
		}catch (Exception ex){
			logger.error(ex.getMessage(), ex);
		}
		return recvedMail;
	}
	/**
	 * ��POP3��ʽ�����ʼ��������е��ʼ�
	 */
	private void pop3RecvMail(){
		Store store = null;
		POP3Folder folder = null;
		try{
			//��ȡĬ�ϻỰ
			Properties props = new Properties();
			Session session = Session.getDefaultInstance(props, null);
			if( mailAccount.isRecvSSL() )
				store = session.getStore("pop3s");
			else
				store = session.getStore("pop3");
			
			try{
				store.connect(mailAccount.getRecvAddress(), mailAccount.getRecvPort(), mailAccount.getAccount(), mailAccount.getPassword());
			}catch( AuthenticationFailedException ex){
				mailBox.setLastError("�ʼ�����ʧ�ܣ������ʻ����������");
			}catch(Exception e){
				mailBox.setLastError(e.getMessage());
				throw e;
			}
			
			if( mailBox.getStatus() != MailBox.CREATED_STATUS )
				this.syncPop3MailBoxFolder();
			
			IMailFolder mailFolder = mailBox.getFolder(MailBox.INBOX);
			
			//��ȡĬ���ļ���
			folder = (POP3Folder)store.getFolder(MailBox.INBOX);
			if (folder == null) {
				String message = mailAccount.getAccount() + "�������ô����޷��ҵ�INBOXĿ¼��";
				logger.error(message);
				throw new Exception(message);
			}
			
			List<MailEntry> recvedMail = pop3RecvMail(folder, mailFolder);
			saveMail(recvedMail);
		}catch( AuthenticationFailedException ex){
			mailBox.setLastError("�ʼ�����ʧ�ܣ������ʻ����������");
		}catch (Exception ex){
			mailBox.setLastError("�ʼ�����ʧ�ܣ�" + ex.getMessage());
			logger.error(ex.getMessage(), ex);
		}
		finally{
			//�ͷ���Դ
			try{
				if (folder != null)
					folder.close(false);
				if (store != null)
					store.close();
			}
			catch (Exception e){
			}
		}
	}
	private List<MailEntry> pop3RecvMail(POP3Folder folder, IMailFolder mailFolder){
		List<MailEntry> recvedMail = new ArrayList<MailEntry>();
		try{
			IMailFolder spamFolder = mailBox.getFolder(MailBox.SPAM);
			
			//ʹ��ֻ����ʽ���ռ���
			folder.open(Folder.READ_ONLY);
			
			FetchProfile profile = new FetchProfile();
			profile.add(UIDFolder.FetchProfileItem.UID);
			Message[] messages = folder.getMessages();
			folder.fetch(messages, profile);
			Message message;
			int count = 0;
			for(int i = 0; i < messages.length; i++)
			{
				message = messages[i];
				String uid = folder.getUID(message);
				
				if(mailBox.isNewMail(uid) && message instanceof MimeMessage)
				{
					MailEntry mailEntry = new MailEntry(mailBox);
					mailEntry.setMailMessage((MimeMessage)message, uid);
					mailEntry.getEntity().put("mail_account_id", mailAccount.getEntity().get("id"));
					mailEntry.getEntity().put("mail_account_label", mailAccount.getAccount());
					mailEntry.setMailType(MailEntry.RECV_MAIL);
					mailEntry.addFlag(MailEntry.Flag.RECENT);
					if( SpamManager.isSpamMail(message) )
						mailEntry.setMailFolder(spamFolder);
					else
						mailEntry.setMailFolder(mailFolder);
					
					recvedMail.add(mailEntry);
					count ++;
					if( count > BATCH_SIZE ){
						saveMail(recvedMail);
						count = 0;
						recvedMail.clear();
					}
				}
			}
		}catch (Exception ex){
			logger.error(ex.getMessage(), ex);
		}
		return recvedMail;
	}
	private void sendMail(){
		IMailFolder sendedFolder = mailBox.getFolder(MailBox.SENDED);
		
		List<MailEntry> sendedList = new ArrayList<MailEntry>();
		Transport transport = null;
		try{
			Properties props = new Properties();
			
			if( mailAccount.isSendSSL() ){
				props.put("mail.smtps.host", mailAccount.getSendAddress());	//ָ��SMTP������
				props.put("mail.smtps.auth", "true");	//ָ���Ƿ���ҪSMTP��֤
			}
			else {
				props.put("mail.smtp.host", mailAccount.getSendAddress());	//ָ��SMTP������
				props.put("mail.smtp.auth", "true");	//ָ���Ƿ���ҪSMTP��֤
			}
			
			try{
				PopupAuthenticator popAuthenticator = new PopupAuthenticator(mailAccount.getAccount(), mailAccount.getPassword());
				Session mailSession = Session.getInstance(props, popAuthenticator);
				
				mailBox.setMailSession(mailSession);
				
				if( mailAccount.isSendSSL() )
					transport = mailSession.getTransport("smtps");
				else
					transport = mailSession.getTransport("smtp");
				transport.connect(mailAccount.getSendAddress(), mailAccount.getSendPort(), mailAccount.getAccount(), mailAccount.getPassword());
			}
			catch(Exception e){
				mailBox.setLastError(e.getMessage());
				throw e;
			}
			List<MailEntry> sendList = null;
			SessionContext ctx = null;
	    	try {
	    		ctx = EntityManagerUtil.currentContext();
	    		
	    		IMailFolder outbox = mailBox.getFolder(MailBox.OUTBOX);
				if( outbox != null ){
					sendList = outbox.getMails();
				}
	    		
			} catch(Exception e){
				logger.error(e.getMessage(), e);
			}finally{
				EntityManagerUtil.closeSession(ctx);
			}
			
			Message message;
			for(MailEntry mailEntry : sendList){
				message = mailEntry.getMailMessage();
				try {
					transport.sendMessage(message, message.getAllRecipients());
					mailEntry.setMailFolder(sendedFolder);
					sendedList.add(mailEntry);
				} catch (MessagingException e) {
				}
			}
		}
		catch(Exception e){
			
		}
		finally{
			if( transport != null ){
				try {
					transport.close();
				} catch (MessagingException e) {
				}
			}
		}
		saveMail(sendedList);
	}
	public void setState(State state){
		this.state = state;
		if( this.state == State.Stoped || this.state == State.Compledted ){
			if( listeners != null )
				listeners.clear();
		}
	}
	public State getState(){
		return this.state;
	}
	public void addMessageListener(IMessageListener listener){
		if( listeners == null )
			listeners = new ArrayList<IMessageListener>();
		listeners.add(listener);
	}
	public void removeMessageListener(IMessageListener listener){
		if( listeners != null )
			listeners.remove(listener);
	}
	public void dispatchMessage(String message){
		if( listeners == null )
			return;
		for(IMessageListener listener : listeners ){
			listener.onMessage(message);
		}
	}
}
