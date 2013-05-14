package ime.mail.mailbox;

import ime.mail.MailEntry;

import org.directwebremoting.util.Logger;
import org.hibernate.Transaction;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;

public class MailFolder implements IMailFolder {
	private static Logger logger = Logger.getLogger(MailFolder.class);
	
	public static final String ENTITY_NAME = "ML_MailFolder";
	
	public static final long SYSTEM_FOLDER = 1L;
	public static final long SERVER_FOLDER = 2L;
	public static final long USER_FOLDER   = 3L;
	
	private Map<String, Object> entity;
	private IMailFolder parent;
	private IMailBox mailBox;
	List<IMailFolder> children;
	
	public MailFolder(IMailBox mailBox, Map<String, Object> entity){
		this.mailBox = mailBox;
		this.entity = entity;
	}
	public long getId(){
		return (Long)entity.get("id");
	}
	@Override
	public String getName(){
		return (String)entity.get("name");
	}
	@Override
	public void setName(String name) {
		entity.put("name", name);
	}
	@Override
	public Object getField(String name){
		return entity.get(name);
	}
	@Override
	public void setField(String name, Object value){
		entity.put(name, value);
	}
	public void setType(long type){
		entity.put("type", type);
	}
	public long getType(){
		Long type = (Long)entity.get("type");
		if( type == null )
			return 0;
		return type.longValue();
	}
	@SuppressWarnings("unchecked")
	@Override
	public List<MailEntry> getMails() {
		List<MailEntry> result = new ArrayList<MailEntry>();
		
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		StringBuilder sb = new StringBuilder();
    		sb.append("from ")
    		  .append(MailEntry.ENTITY_NAME)
    		  .append(" where folder_id = :folderId");
    		
    		org.hibernate.Query query = hsession.createQuery(sb.toString());
    		query.setParameter("folderId", entity.get("id"));
    		
    		List list = query.list();
    		MailEntry mail;
    		for(Object item : list){
    			mail = new MailEntry(mailBox, (Map<String, Object>)item);
    			result.add(mail);
    		}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
		return result;
	}
	@Override
	public void create(IMailFolder parent) throws Exception {
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();

    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				entity.put("mail_box_id", mailBox.getId());
				entity.put("mail_box_label", mailBox.getName());
				
				if( parent != null ){
					entity.put("parent_label", parent.getName());
					entity.put("parent_id", parent.getId());
				}
				else {
					if( entity.containsKey("parent_label"))
						entity.remove("parent_label");
					if( entity.containsKey("parent_id") )
						entity.remove("parent_id");
				}
				
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
		this.parent = parent;
		if( parent != null )
			parent.getChildren().add(this);
		else
			mailBox.getFolders().add(this);
	}
	@SuppressWarnings("unchecked")
	@Override
	public List<IMailFolder> getChildren() {
		if( this.children != null )
			return this.children;
		
		List<IMailFolder> result = new ArrayList<IMailFolder>();
		
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		StringBuilder sb = new StringBuilder();
    		sb.append("from ")
    		  .append(MailFolder.ENTITY_NAME)
    		  .append(" where parent_id = :parentId and mail_box_id = :mailBoxId");
    		
    		org.hibernate.Query query = hsession.createQuery(sb.toString());
    		query.setParameter("mailBoxId", entity.get("id"));
    		query.setParameter("parentId", this.getId());
    		
    		List list = query.list();
    		IMailFolder folder;
    		for(Object item : list){
    			folder = new MailFolder(mailBox, (Map<String, Object>)item);
    			result.add(folder);
    		}
		} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
		this.children = result;
		return result;
	}
	@Override
	public IMailBox getMailBox() {
		return mailBox;
	}
	@Override
	public void setMailBox(IMailBox mailBox) {
		this.mailBox = mailBox;
	}

	@Override
	public IMailFolder getParent() {
		return parent;
	}
	@Override
	public void setParent(IMailFolder parent) {
		this.parent = parent;
	}
	@Override
	public void remove() throws Exception {
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();

    		org.hibernate.Session hsession = ctx.getHibernateSession();

    		Transaction tx = hsession.getTransaction();
    		boolean owner_tx = !tx.isActive();
			try {
				if( owner_tx )
					tx.begin();
				
				hsession.delete(ENTITY_NAME, entity);
				
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
		if( this.parent != null )
			this.parent.getChildren().remove(this);
		else
			this.mailBox.getFolders().remove(this);
	}
	@Override
	public void save() throws Exception {
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();

    		org.hibernate.Session hsession = ctx.getHibernateSession();
    		
			entity.put("mail_box_id", mailBox.getId());
			entity.put("mail_box_label", mailBox.getName());

    		if( parent != null ){
				entity.put("parent_label", parent.getName());
				entity.put("parent_id", parent.getId());
			}
			else {
				if( entity.containsKey("parent_label"))
					entity.remove("parent_label");
				if( entity.containsKey("parent_id") )
					entity.remove("parent_id");
			}
    		
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
