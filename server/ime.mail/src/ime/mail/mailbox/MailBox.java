package ime.mail.mailbox;

import ime.document.DocumentStorage;
import ime.mail.MailAccount;
import ime.mail.MailEntry;

import java.io.BufferedReader;
import java.io.BufferedWriter;
import java.io.File;
import java.io.FileReader;
import java.io.FileWriter;
import java.io.IOException;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import javax.mail.Session;

import org.directwebremoting.util.Logger;
import org.hibernate.Transaction;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

public class MailBox implements IMailBox {
	private static Logger logger = Logger.getLogger(MailBox.class);
	
	public static final String ENTITY_NAME = "ML_MailBox";
	
	//接收邮件文件夹
	public static final String INBOX  = "INBOX";
	//发送邮件文件夹
	public static final String OUTBOX = "OUTBOX";
	//已发送邮件文件夹
	public static final String SENDED = "SENDED";
	//草稿邮件文件夹
	public static final String DRAFT = "DRAFT";
	//垃圾邮件文件夹
	public static final String SPAM  = "SPAM";
	//已删除邮件文件夹
	public static final String DELETED = "DELETED";
	
	public static final long CREATED_STATUS = 1L;
	public static final long REMOVED_STATUS = 2L;
	
	private Map<String, Object> entity;
	private MailAccount mailAccount;
	private Session mailSession;
	private List<IMailFolder> folders;
	private Set<String> uids;
	
	public MailBox(Map<String, Object> entity, MailAccount mailAccount){
		this.entity = entity;
		this.mailAccount = mailAccount;
	}
	public long getId(){
		Long id = (Long)entity.get("id");
		if( id != null )
			return id.longValue();
		else
			return 0L;
	}
	public String getName(){
		return (String)entity.get("name");
	}
	public long getStatus(){
		Long status = (Long)entity.get("status");
		if( status != null )
			return status.longValue();
		else
			return 0L;
	}
	public void setStatus(long status){
		entity.put("status", status);
	}
	public void setLastError(String error){
		entity.put("last_error", error);
	}
	@SuppressWarnings("unchecked")
	@Override
	public List<IMailFolder> getFolders() {
		if( this.folders != null )
			return this.folders;
		
		List<IMailFolder> result = new ArrayList<IMailFolder>();
		
		Map<Long, MailFolder> idMap = new HashMap<Long, MailFolder>();
		List list = null;
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		StringBuilder sb = new StringBuilder();
    		sb.append("from ")
    		  .append(MailFolder.ENTITY_NAME)
    		  .append(" where mail_box_id = :mailBoxId");
    		
    		org.hibernate.Query query = hsession.createQuery(sb.toString());
    		query.setParameter("mailBoxId", entity.get("id"));
    		
    		list = query.list();
		} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
		if( list != null ){
			MailFolder folder;
			for(Object item : list){
				folder = new MailFolder(this, (Map<String, Object>)item);
				idMap.put(folder.getId(), folder);
			}
			Long parentId;
			for(MailFolder fd : idMap.values()){
				parentId = (Long)fd.getField("parent_id");
				if( parentId == null || parentId == 0L )
					result.add(fd);
				else {
					MailFolder parent = idMap.get(parentId);
					if( parent != null ){
						fd.setParent(parent);
						if( parent.children == null )
							parent.children = new ArrayList<IMailFolder>();
						parent.children.add(fd);
					}
				}
			}
		}
		this.folders = result;
		return result;
	}

	@Override
	public IMailFolder getFolder(String name) {
		if( this.folders == null )
			this.folders = this.getFolders();
		for(IMailFolder folder : this.folders ){
			if( name.equals(folder.getName()) )
				return folder;
		}
		return null;
	}

	@Override
	public IMailFolder createFolder(String name, long type) throws Exception {
		Map<String, Object> entity = new HashMap<String, Object>();
		entity.put("name", name);
		MailFolder folder = new MailFolder(this, entity);
		folder.setType(type);
		folder.create(null);
		if( folders != null )
			folders.add(folder);
		return folder;
	}

	@Override
	public void removeFolder(String name) throws Exception{
		IMailFolder folder = getFolder(name);
		if( folder != null )
			folder.remove();
	}

	@Override
	public MailAccount getMailAccount() {
		return mailAccount;
	}
	@Override
	public void setMailSession(Session mailSession) {
		this.mailSession = mailSession;
	}
	@Override
	public Session getMailSession() {
		return mailSession;
	}
	
	public boolean isNewMail(String uid){
		if( uids == null )
			loadUids();
		return !uids.contains(uid);
	}
	private String getUidsFile(){
		StringBuilder sb = new StringBuilder();
		sb.append(MailEntry.ROOT_DIR).append("/uids/").append(mailAccount.getAccount()).append(".uids");
		return DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, sb.toString());
	}
	private void loadUids(){
		if( uids == null )
			uids = new HashSet<String>();
		else
			uids.clear();
		
		BufferedReader reader = null;
		try {
			reader = new BufferedReader(new FileReader(getUidsFile()));
			String line;
			while( (line = reader.readLine()) != null ){
				uids.add(line);
			}
		} catch (Exception e) {
		}
		finally{
			if( reader != null ){
				try {
					reader.close();
				} catch (IOException e) {
				}
			}
		}
	}
	public void saveUids(Set<String> newUids){
		BufferedWriter writer = null;
		try {
			File file = new File(getUidsFile());
			file.getParentFile().mkdirs();
			
			writer = new BufferedWriter(new FileWriter(file, true));
			for(String uid : newUids){
				writer.write(uid);
				writer.write("\n");
			}
			writer.flush();
		} catch (Exception e) {
		}
		finally{
			if( writer != null ){
				try {
					writer.close();
				} catch (IOException e) {
				}
			}
		}
	}
	public void save() throws Exception{
		//保存邮件记录
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();

    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				if( entity.containsKey("id") )
					hsession.merge(ENTITY_NAME, entity);
				else
					hsession.persist(ENTITY_NAME, entity);
				
				if( owner_tx )
					tx.commit();
			} catch (Exception e) {
				if( owner_tx )
					tx.rollback();
				throw e;
			}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
			throw e;
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
	}
}
