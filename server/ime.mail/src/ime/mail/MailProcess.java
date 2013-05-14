package ime.mail;

import ime.core.services.DynamicEntityService;
import ime.mail.mailbox.MailBox;
import ime.mail.work.MailWorker;

import java.util.List;
import java.util.Map;
import java.util.concurrent.ThreadPoolExecutor;

import org.directwebremoting.util.Logger;
import org.hibernate.Query;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

/**
 * 邮件处理主程序
 * @author HongHao
 *
 */
public class MailProcess {
	private static Logger logger = Logger.getLogger(MailProcess.class);
	
	private static MailProcess process;
	private ThreadPoolExecutor threadPool;
	
	protected MailProcess(){
		
	}
	public static MailProcess getInstance(){
		if( process == null ){
			process = new MailProcess();
		}
		
		return process;
	}
	
	@SuppressWarnings("unchecked")
	public void start(){
		SessionContext ctx = null;
    	try {
    		ctx = EntityManagerUtil.currentContext();
    		org.hibernate.Session hsession = ctx.getHibernateSession();
    		
    		Query query = hsession.createQuery("from ML_MailBox");
    		List mailBoxList = query.list();
    		for(Object item : mailBoxList){
    			Map<String, Object> entity = (Map<String, Object>)item;
    			Long accountId = (Long)entity.get("mail_account_id");
    			if( accountId == null )
    				continue;
    			Map<String, Object> accountEntity = DynamicEntityService.getEntity(accountId, "ML_MailAccount");
    			MailAccount mailAccount = new MailAccount(accountEntity);
    			MailBox mailBox = new MailBox(entity, mailAccount);
    			
    			MailWorker worker = new MailWorker(mailBox);
    			new Thread(worker).start();
    		}
    		
    	} catch(Exception e){
			logger.error(e.getMessage(), e);
		}finally{
			EntityManagerUtil.closeSession(ctx);
		}
	}
	public void stop(){
		
	}
}
