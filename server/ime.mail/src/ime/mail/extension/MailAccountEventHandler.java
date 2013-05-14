package ime.mail.extension;

import java.util.Map;

import org.hibernate.Query;
import org.hibernate.Session;

import ime.core.event.DynamicEntityEvent;
import ime.core.event.IEntityEventHandler;
import ime.mail.MailAccount;

public class MailAccountEventHandler implements IEntityEventHandler {

	@Override
	public Map<String, Object> customerEvent(String entityName, String eventName, Map<String, Object> eventParam, Session session) throws Exception {
		return null;
	}

	@Override
	public void postCreate(DynamicEntityEvent event, Session session) throws Exception {
	}

	@Override
	public void postRemove(DynamicEntityEvent event, Session session) throws Exception {
	}

	@Override
	public void postUpdate(DynamicEntityEvent event, Session session) throws Exception {
	}

	@Override
	public void preCreate(DynamicEntityEvent event, Session session) throws Exception {
		Map<String, Object> entity = event.getEntity();
		String password = (String)entity.get("password");
		if( password != null )
			entity.put("password", MailAccount.encodePassword(password));
	}

	@Override
	public void preRemove(DynamicEntityEvent event, Session session) throws Exception {
	}

	@Override
	public void preUpdate(DynamicEntityEvent event, Session session) throws Exception {
		Map<String, Object> entity = event.getEntity();
		String password = (String)entity.get("password");
		if( password != null ){
			Query query = session.createQuery("select password from ML_MailAccount where id = :id");
			query.setParameter("id", entity.get("id"));
			String dbpass = (String)query.uniqueResult();
			if( !password.equals(dbpass) ){
				entity.put("password", MailAccount.encodePassword(password));
			}
		}
	}

}
