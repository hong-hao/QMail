package ime.mail;

import ime.core.dynamic.DynamicEntity;
import ime.core.dynamic.EntityManager;
import ime.core.dynamic.DynamicEntity.Property;
import ime.core.services.DynamicEntityService;
import ime.core.utils.NumberUtil;
import ime.security.LoginSession;
import ime.security.entity.Principal;
import ime.security.services.PrincipalService;
import ime.xmpp.vysper.VysperServer;

import java.util.ArrayList;
import java.util.Date;
import java.util.HashMap;
import java.util.Iterator;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ConcurrentHashMap;

import org.apache.log4j.Logger;
import org.hibernate.Query;
import org.hibernate.Session;
import org.hibernate.Transaction;
import org.json.JSONArray;
import org.json.JSONObject;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

/**
 * 
 *
 * @author honghao
 *
 */
public class MailManager {
	private static Logger logger = Logger.getLogger(MailManager.class);
	
	//private static String PASS_KEY = "asde*230DLw^3";
	
	private static JSONArray mail_accounts = null;
	private static Map<String, String> recv_tasks = new ConcurrentHashMap<String, String>();
	
	private static MailManager instance;
	
	public static MailManager getInstance(){
		if( instance == null ){
			instance = new MailManager();
		}
		
		return instance;
	}
	/**
	 * 装载邮件帐号
	 */
	@SuppressWarnings("unchecked")
	public static void loadMailAccounts(){
		try{
			JSONArray mail_accounts = new JSONArray();
			
			SessionContext ctx = null;
			try {
				ctx = EntityManagerUtil.currentContext();
	
				org.hibernate.Session hsession = ctx.getHibernateSession();
				Query query = hsession.createQuery("select a, b from ML_MailAccount a, ML_MailBox b where b.mail_account_id = a.id and a.is_enabled=:is_enabled");
				query.setParameter("is_enabled", Boolean.TRUE);
				List list = query.list();
				JSONObject account_entry;
				for(Object item : list){
					Object[] record = (Object[]) item;       
					Map<String, Object> account = (Map<String, Object>)record[0];
					Map<String, Object> mailbox = (Map<String, Object>)record[1];
					
					account_entry = new JSONObject(account);
					account_entry.put("mailbox_status", mailbox.get("status"));
					account_entry.put("mailbox_error", mailbox.get("last_error"));
					account_entry.put("mailbox_owner_id", mailbox.get("owner_user_id"));
					String config = (String)mailbox.get("config_data");
					if( config != null ){
						JSONObject cfg = null;
						try{
							cfg = new JSONObject(config);
							if( cfg.has("distribution_policy") )
								account_entry.put("distribution_policy", cfg.get("distribution_policy"));
						}
						catch(Exception e){
						}
					}
					mail_accounts.put(account_entry);
				}
			} catch (Exception e) {
				logger.error(e.getMessage(), e);
				throw e;
			} finally {
				EntityManagerUtil.closeSession(ctx);
			}
			
			MailManager.mail_accounts = mail_accounts;
		}
		catch(Exception e){
			logger.error(e.getMessage(), e);
		}
	}
	
	/**
	 * 获取账户所有的UID
	 * @param mail_account
	 * @return
	 * @throws Exception
	 */
	@SuppressWarnings("unchecked")
	public List getMailAccountUids(String mail_account) throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();	
			sb.setLength(0);
			sb.append("select a.mail_uid from ML_MailEntry a, ML_MailAccount b where a.mail_account_id=b.id and b.account=:account");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("account", mail_account);
			return query.list();
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 创建邮件帐号
	 * @param account 帐号数据
	 * @param config 邮箱配置数据, JSON格式
	 */
	public void createMailAccount(Map<String, Object> account, String config)throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();

			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				
				Map<String, Object> entityMap = convertMap(account, "ML_MailAccount");
				entityMap.put("owner_user_id", LoginSession.currentPrincipalId());
				entityMap.put("owner_user_name", LoginSession.currentLoginName());
				entityMap.put("is_enabled", true);
				hsession.persist("ML_MailAccount", entityMap);
				
				Map<String, Object> mailbox = new HashMap<String, Object>();
				mailbox.put("mail_account_id", entityMap.get("id"));
				mailbox.put("mail_account_label", entityMap.get("account"));
				mailbox.put("owner_user_id", entityMap.get("owner_user_id"));
				mailbox.put("owner_user_name", entityMap.get("owner_user_name"));
				mailbox.put("config_data", config);
				mailbox.put("name", account.get("account"));
				
				hsession.persist("ML_MailBox", mailbox);
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 修改邮件帐号
	 * @param account 帐号数据
	 * @param config 邮箱配置数据, JSON格式
	 */
	@SuppressWarnings("unchecked")
	public void updateMailAccount(Map<String, Object> account, String config)throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();

			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				Map<String, Object> entityMap = convertMap(account, "ML_MailAccount");
				hsession.merge("ML_MailAccount", entityMap);
				
				StringBuilder sb = new StringBuilder();
				sb.append("from ML_MailBox where mail_account_id=:mail_account_id");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("mail_account_id", entityMap.get("id"));
				Map<String, Object> mailbox = (Map<String, Object>)query.uniqueResult();
				mailbox.put("config_data", config);
				
				hsession.merge("ML_MailBox", mailbox);
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 修改邮件帐号
	 * @param accountId 帐号ID
	 */
	public void removeMailAccount(Long accountId)throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();

			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				
				StringBuilder sb = new StringBuilder();
				sb.append("update ML_MailAccount set is_enabled=:is_enabled where id=:id");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("is_enabled", Boolean.FALSE);
				query.setParameter("id", accountId);
				query.executeUpdate();
				
				sb.setLength(0);
				sb.append("update ML_MailBox set status=:status where mail_account_id=:id");
				query = hsession.createQuery(sb.toString());
				query.setParameter("status", 2L);
				query.setParameter("id", accountId);
				query.executeUpdate();
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 判断账号时候存在，存在判断是否启用
	 * 
	 * @param account
	 *            帐号
	 */
	@SuppressWarnings("unchecked")
	public String existMailAccount(String account) throws Exception {
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();
			JSONObject reslut = new JSONObject();

			StringBuilder sb = new StringBuilder();
			sb.append("select is_enabled from ML_MailAccount where account=:account and owner_user_id=:owner_user_id");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("account", account);
			query.setParameter("owner_user_id", LoginSession.currentPrincipalId());
			List<Object> list = query.list();
			if (list.isEmpty()) {
				reslut.put("isExist", false);
			} else {
				reslut.put("isExist", true);
				Boolean is_enabled = (Boolean) list.get(0);
				reslut.put("is_enabled", is_enabled);
			}

			return reslut.toString();
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 判断账号时候存在，存在判断是否启用
	 * 
	 * @param _account
	 *            帐号
	 */
	@SuppressWarnings("unchecked")
	public String enableMailAccount(String _account)throws Exception {
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			org.hibernate.Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.append("select id, is_enabled from ML_MailAccount where account=:account and owner_user_id=:owner_user_id");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("account", _account);
			query.setParameter("owner_user_id", LoginSession.currentPrincipalId());
			List<Object> list = query.list();
			if (list.isEmpty()) {
				throw new Exception("账号不存在");
			} else {
				Object[] record = (Object[]) list.get(0);
				Long id = (Long) record[0];
				Boolean is_enabled = (Boolean) record[1];
				if (is_enabled)
					throw new Exception("账号已启用");
				
				Transaction tx = hsession.getTransaction();
				boolean owner_tx = !tx.isActive();
				
				try {
					if (owner_tx)
						tx.begin();
					sb.setLength(0);
					sb.append("update ML_MailAccount set is_enabled=:is_enabled where id=:id");
					query = hsession.createQuery(sb.toString());
					query.setParameter("is_enabled", Boolean.TRUE);
					query.setParameter("id", id);
					query.executeUpdate();

					sb.setLength(0);
					sb.append("update ML_MailBox set status=:status where mail_account_id=:id");
					query = hsession.createQuery(sb.toString());
					query.setParameter("status", 1L);
					query.setParameter("id", id);
					query.executeUpdate();
					if (owner_tx)
						tx.commit();
				} catch (Exception e) {
					if (owner_tx)
						tx.rollback();
					throw e;
				}
				sb.setLength(0);
				sb.append("select a, b from ML_MailAccount a, ML_MailBox b where b.mail_account_id = a.id and a.id=:id");
				query = hsession.createQuery(sb.toString());
				query.setParameter("id", id);
				list = query.list();
				if(list.isEmpty())
					return null;
				
				Object[] _record = (Object[]) list.get(0);
				Map<String, Object> account = (Map<String, Object>) _record[0];
				Map<String, Object> mailbox = (Map<String, Object>) _record[1];

				JSONObject account_entry = new JSONObject(account);
				account_entry.put("mailbox_status", mailbox.get("status"));
				account_entry.put("mailbox_error", mailbox.get("last_error"));
				account_entry.put("mailbox_owner_id", mailbox.get("owner_user_id"));
				String config = (String) mailbox.get("config_data");
				if (config != null) {
					JSONObject cfg = null;
					try {
						cfg = new JSONObject(config);
						if (cfg.has("distribution_policy"))
							account_entry.put("distribution_policy", cfg.get("distribution_policy"));
					} catch (Exception e) {
					}
				}
				mail_accounts.put(account_entry);
				
				
				return account_entry.toString();
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 更新邮件帐号
	 */
	public void reloadMailAccount(){
		loadMailAccounts();
	}
	
	/**
	 * 获取与当前用户相关的邮件账户信息
	 * @return JSON字符串，定义：
	 * [
	 * 		{
	 * 			account : "",	//邮件帐号
	 * 			name : "", 		//邮件名称
	 * 			password : "",	//帐号密码（密文）
	 * 			send_address : "",	//SMTP服务器地址
	 * 			send_port : 25,		//SMTP服务器端口
	 * 			is_send_ssl : false,//SMTP服务器是否采用SSL
	 * 			recv_type : "POP3" | "IMAP", //接收服务器类型
	 * 			recv_address : "",  //接收服务器地址
	 * 			recv_port : 110, //接收服务器端口
	 * 			is_recv_ssl : false, //接收服务器是否采用SSL
	 * 			distribution_policy : { //邮件分配策略，参见MailDistributionPolicy中的定义
	 * 				...
	 * 			}
	 * 		},
	 * 		...
	 * ]
	 */
	public String getUserMailAccounts(){
		JSONArray mail_accounts = MailManager.mail_accounts;
		if( mail_accounts == null )
			return "";
		Long currentPrincipalId = (Long)LoginSession.getCurrentAttribute(LoginSession.LOGIN_PRINCIPAL_ID);
		
		JSONArray ret = new JSONArray();
		for(int i = 0; i < mail_accounts.length(); i++ ){
			JSONObject account;
			account = mail_accounts.optJSONObject(i);
			if( account == null )
				continue;
			if( isAssociated(currentPrincipalId, account) )
				ret.put(account);
		}
		return ret.toString();
	}
	
	/**
	 * 获取指定人员的邮件账户信息
	 * @param currentPrincipalId
	 * @return
	 */
	public String getPrincipalMailAccounts(Long currentPrincipalId){
		JSONArray mail_accounts = MailManager.mail_accounts;
		if( mail_accounts == null )
			return "";
		
		JSONArray ret = new JSONArray();
		for(int i = 0; i < mail_accounts.length(); i++ ){
			JSONObject account;
			account = mail_accounts.optJSONObject(i);
			if( account == null )
				continue;
			if( isAssociated(currentPrincipalId, account) )
				ret.put(account);
		}
		return ret.toString();
	}
	
	/**
	 * 判断用户是否与邮件相关联
	 * @param principalId
	 * @param account
	 * @return
	 */
	private boolean isAssociated(Long principalId, JSONObject account){
		if( principalId.equals(account.opt("mailbox_owner_id")) )
			return true;
		JSONObject distribution_policy = account.optJSONObject("distribution_policy");
		if(distribution_policy == null)
			return false;
		JSONArray managers = distribution_policy.optJSONArray("managers");
		if( managers != null && inUsers(principalId, managers) )
			return true;
		JSONArray distributor = distribution_policy.optJSONArray("distributor");
		if( distributor != null && inUsers(principalId, distributor) )
			return true;
		
		return false;
	}
	private boolean inUsers(Long principalId, JSONArray users){
		JSONObject obj;
		for(int i = 0; i < users.length(); i++){
			obj = users.optJSONObject(i);
			if( obj == null )
				continue;
			if( principalId.equals(obj.optLong("id")) )
				return true;
		}
		return false;
	}
	/**
	 * 获取所有的邮件帐号
	 * @return JSON字符串，定义同getUserMailAccounts()
	 */
	public String getAllMailAccounts(){
		if( mail_accounts != null )
			return mail_accounts.toString();
		else
			return "";
	}
	
	/**
	 * 请求更新邮箱的分配策略
	 * @param account 邮箱帐号
	 */
	public void updateDistributionPolicy(String account){
		MailDistributionPolicy.getInstance().reloadPolicy();
	}
	/**
	 * 请求启动邮件接收过程
	 * @param account 邮件帐号
	 * @return 
	 * 	1.允许客户端接收时返回  "OK:消息订阅地址"
	 *  2.已有其他客户端正在处理时返回  "JOIN:消息订阅地址"
	 */
	public String requestRecvTask(String account){
		if( recv_tasks.containsKey(account) )
			return "JOIN:" + recv_tasks.get(account);
		else {
			String pubId = UUID.randomUUID().toString();
			recv_tasks.put(account, pubId);
			return "OK:" + pubId;
		}
	}
	
	/**
	 * 客户端已完成邮件的接收
	 * @param account 正在接收的邮件帐号
	 */
	public void recvTaskFinished(String account){
		//TODO 通知其他用户邮件接收任务已完成 
		recv_tasks.remove(account);
	}
	
	public void importMailsList(List<Map<String, Object>> mails)throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();
			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();

				for(Map<String, Object> mailRecord : mails){
					hsession.persist("ML_MailEntry", mailRecord);
				}
				//TODO 处理邮件联系人
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 从客户端向服务端同步邮件信息
	 * @param mails 当前用户未同步的邮件信息
	 */
	@SuppressWarnings("unchecked")
	public List<String> syncUserMails(List<Map<String, Object>> mails) throws Exception{
		if (mails == null || mails.isEmpty())
			return null;
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			Long principal_id = LoginSession.currentPrincipalId();
			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				for (Iterator<Map<String, Object>> iterator = mails.iterator(); iterator.hasNext();) {
					Map<String, Object> mail = iterator.next();
					mail.remove("id");

					Map<String, Object> entityMap = new HashMap<String, Object>();
					boolean isUpdate = false;
					if (mail.containsKey("mail_uid") && !isNull(mail.get("mail_uid"))) {
						// 先查找在邮件视图中对应的邮件视图 判断message_id是否存在 存在 说明是处理人员在处理
						sb.setLength(0);
						sb.append("select b.id,b.mail_account_id,b.mail_account_label from ML_MailBoxView a, ML_MailEntry b where b.id=a.mail_entry_id and a.is_owner=:is_owner and a.operator_id=:operator_id and b.mail_uid=:mail_uid");
						Query query = hsession.createQuery(sb.toString());
						query.setParameter("is_owner", Boolean.TRUE);
						query.setParameter("operator_id", principal_id);
						query.setParameter("mail_uid", mail.get("mail_uid"));
						List<Object> ms = query.list();
						if (!ms.isEmpty()) {
							isUpdate = true;
							Object[] record = (Object[]) ms.get(0);
							entityMap.put("id", record[0]);
							entityMap.put("mail_account_id", record[1]);
							entityMap.put("mail_account_label", record[2]);
						} else {
							Long mail_account_id = null;
							if (mail.containsKey("mail_account") && !isNull(mail.get("mail_account"))) {
								sb.setLength(0);
								sb.append("select id, name from ML_MailAccount where account=:account");
								query = hsession.createQuery(sb.toString());
								query.setParameter("account", mail.get("mail_account"));
								List<Object> list = query.list();
								if (list.isEmpty()) {
									if ("SENDED".equals(mail.get("folder")) && !isNull(mail.get("operator_id"))) {
										sb.setLength(0);
										sb.append("select id, name from ML_MailAccount where account=:account");
										query = hsession.createQuery(sb.toString());
										query.setParameter("account", mail.get("mail_account"));
										list = query.list();
										if (list.isEmpty())
											throw new Exception("邮件账号不存在");
									} else if ("DSBOX".equals(mail.get("folder")) || "OUTBOX".equals(mail.get("folder"))&& !isNull(mail.get("operator_id"))) {
										continue;
									} else
										throw new Exception("邮件账号不存在");
								}
								Object[] record = (Object[]) list.get(0);
								mail_account_id = (Long) record[0];
								String account_name = (String) record[1];
								entityMap.put("mail_account_id",mail_account_id);
								entityMap.put("mail_account_label",account_name);
							}
							sb.setLength(0);
							sb.append("select id, mail_account_id from ML_MailEntry where mail_uid=:mail_uid");
							query = hsession.createQuery(sb.toString());
							query.setParameter("mail_uid", mail.get("mail_uid"));
							List list = query.list();
							if (list.isEmpty()) {
								isUpdate = false;
							} else {
								Long id = null;
								if (list.size() > 1) {
									Iterator<Object> it = list.iterator();
									while (it.hasNext()) {
										Object[] record = (Object[]) it.next();
										if (record[1] == null) {
											continue;
										} else if (((Long) record[1]).equals(mail_account_id)) {
											id = (Long) record[1];
											break;
										}
									}
								}
								if (id == null)
									entityMap.put("id",((Object[]) list.get(0))[0]);
								else
									entityMap.put("id", id);
								isUpdate = true;
							}
						}
					} else
						throw new Exception("信息不准确");

					Map<String, Property> propertys = getEntityPropertys("ML_MailEntry");
					Iterator<Map.Entry<String, Property>> it = propertys.entrySet().iterator();
					while (it.hasNext()) {
						Map.Entry<String, Property> entry = it.next();
						Property p = entry.getValue();
						if ("long".equals(p.type)) {
							Object value = mail.get(entry.getKey());
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey(), Double.valueOf(value.toString()).longValue());
						} else if ("boolean".equals(p.type)) {
							Object value = mail.get(entry.getKey());
							if (isNull(value))
								continue;
							boolean b = false;
							if ("true".equals(value.toString()))
								b = true;
							else if ("false".equals(value.toString()))
								b = false;
							else if ("0".equals(value.toString()))
								b = false;
							else
								b = true;

							entityMap.put(entry.getKey(), b);
						} else if ("relation".equals(p.type)) {
							Object value = mail.get(entry.getKey() + "_id");
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey() + "_id", Double.valueOf(value.toString()).longValue());

							value = mail.get(entry.getKey() + "_label");
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey() + "_label", value);
						} else if ("principal".equals(p.type)) {
							Object value = mail.get(entry.getKey() + "_id");
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey() + "_id", Double.valueOf(value.toString()).longValue());

							value = mail.get(entry.getKey() + "_name");
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey() + "_name", value);
						} else if ("string".equals(p.type)) {
							Object value = mail.get(entry.getKey());
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey(), value.toString());
						} else {
							Object value = mail.get(entry.getKey());
							if (isNull(value))
								continue;
							entityMap.put(entry.getKey(), value);
						}
					}

					is_new_contact = false;// 是否新联系人
					Map<String, Object> mlContactMap = null;
					if (!entityMap.containsKey("customer_id")) {
						// 确定联系人信息
						mlContactMap = findMLContact(mail, hsession);
						entityMap.put("customer_id", mlContactMap.get("id"));
						entityMap.put("customer_label", mlContactMap.get("name"));
					} else {
						Long mail_type = (Long) entityMap.get("mail_type");
						if (mail_type == 1 && "SENDED".equals(mail.get("folder"))) {
							sb.setLength(0);
							sb.append("update ML_Contact set last_contact_time=:last_contact_time where id=:id");
							Query query = hsession.createQuery(sb.toString());
							query.setParameter("last_contact_time", new Date());
							query.setParameter("id", (Long) entityMap.get("customer_id"));
							query.executeUpdate();
						}
					}

					if (mail.containsKey("customer_grade")) {
						Long customer_grade = 0L;
						if (!isNull(mail.get("customer_grade")))
							customer_grade = Double.valueOf(mail.get("customer_grade").toString()).longValue();
						if (!customer_grade.equals((Long) mlContactMap.get("grade"))) {
							sb.setLength(0);
							sb.append("update ML_Contact set grade=:grade where id=:id");
							Query query = hsession.createQuery(sb.toString());
							query.setParameter("grade", customer_grade);
							query.setParameter("id", mlContactMap.get("id"));
							query.executeUpdate();
						}
					}

					if (mail.containsKey("folder")&& !isNull(mail.get("folder"))) {
						String folder = mail.get("folder").toString();
						sb.setLength(0);
						sb.append("from ML_MailFolder where name=:name");
						Query query = hsession.createQuery(sb.toString());
						query.setParameter("name", folder);
						List<Object> list = query.list();
						if (list.isEmpty()) {
							Map<String, Object> mailBoxMap = findMLMailBox((Long) entityMap.get("mail_account_id"),hsession);
							Map<String, String> folderMap = new HashMap<String, String>();
							folderMap.put("name", folder);
							folderMap.put("mail_box_id", getValue(mailBoxMap.get("id")));
							folderMap.put("mail_box_label", getValue(mailBoxMap.get("name")));
							Map<String, Object> folderEntity = DynamicEntityService.createEntity("ML_MailFolder", folderMap,null);
							entityMap.put("folder_id", folderEntity.get("id"));
							entityMap.put("folder_label", folderEntity.get("name"));
						} else {
							Map<String, Object> folderEntity = (Map<String, Object>) list.get(0);
							entityMap.put("folder_id", folderEntity.get("id"));
							entityMap.put("folder_label", folderEntity.get("name"));
						}
					}

					if (isUpdate) {
						hsession.merge("ML_MailEntry", entityMap);
						// 更改邮件对应邮件视图中的 is_seen is_handled
						if (entityMap.containsKey("is_seen") || entityMap.containsKey("is_handled")) {
							sb.setLength(0);
							sb.append("update ML_MailBoxView set is_seen=:is_seen,seen_time=:seen_time,is_handled=:is_handled,handle_time=:handle_time ");
							sb.append(" where mail_entry_id=:mail_entry_id and is_owner=:is_owner and operator_id=:operator_id");
							Query query = hsession.createQuery(sb.toString());
							query.setParameter("mail_entry_id", entityMap.get("id"));
							query.setParameter("is_owner", Boolean.TRUE);
							query.setParameter("operator_id", principal_id);
							if (((Boolean) entityMap.get("is_seen") == null) ? false: (Boolean) entityMap.get("is_seen")) {
								query.setParameter("is_seen", Boolean.TRUE);
								query.setParameter("seen_time", new Date());
							} else {
								query.setParameter("is_seen", Boolean.FALSE);
								query.setParameter("seen_time", null);
							}
							if (((Boolean) entityMap.get("is_handled") == null) ? false: (Boolean) entityMap.get("is_handled")) {
								query.setParameter("is_handled", Boolean.TRUE);
								query.setParameter("handle_time", new Date());
							} else {
								query.setParameter("is_handled", Boolean.FALSE);
								query.setParameter("handle_time", null);
							}
							query.executeUpdate();
						}
					} else {
						hsession.persist("ML_MailEntry", entityMap);
						entityMap.put("is_new_contact", is_new_contact);

						Long mail_type = (Long) entityMap.get("mail_type");
						if (mail_type != MailEntry.DRAFT_MAIL) {
							String folder = getValue(mail.get("folder"));
							// 如果是审核邮件 不进行分发邮件。邮件视图包括自身和目标人员
							if ("DSBOX".equals(folder)) {
								Map<String, String> _entityMap = new HashMap<String, String>();
								_entityMap.put("operator_id", getValue(mail.get("owner_user_id")));
								_entityMap.put("mail_entry_id",getValue(entityMap.get("id")));
								_entityMap.put("mail_entry_label",getValue(entityMap.get("subject")));
								_entityMap.put("recv_time", getValue(new Date().getTime()));
								_entityMap.put("is_owner",getValue(Boolean.TRUE));
								_entityMap.put("is_new_contact",getValue(is_new_contact));

								DynamicEntityService.createEntity("ML_MailBoxView", _entityMap, null);

								_entityMap = new HashMap<String, String>();
								_entityMap.put("operator_id", getValue(mail.get("reviewer_id")));
								_entityMap.put("operator_name", getValue(mail.get("reviewer_name")));
								_entityMap.put("mail_entry_id",getValue(entityMap.get("id")));
								_entityMap.put("mail_entry_label",getValue(entityMap.get("subject")));
								_entityMap.put("recv_time", getValue(new Date().getTime()));
								_entityMap.put("is_owner",getValue(Boolean.TRUE));
								_entityMap.put("is_new_contact",getValue(is_new_contact));
								DynamicEntityService.createEntity("ML_MailBoxView", _entityMap, null);

								// 新待审邮件通知
								Principal from = PrincipalService.getPrincipal(Double.valueOf(mail.get("owner_user_id").toString()).longValue());
								Principal to = PrincipalService.getPrincipal((Long) entityMap.get("reviewer_id"));
								sb.setLength(0);
								sb.append("<message type=\"MailDsBoxDeliver\" >")
									.append("<subject>").append(from.getName()).append("向您转交了一个新待审邮件通知").append("</subject>").append("<body>")
									.append("<message>").append(from.getName()).append("向您转交了一个新待审邮件通知").append("</message>").append("</body>")
									.append("<icon></icon>").append("</message>");
								AddMessage(to.getLoginId() + "@" + to.getDomainName(), sb.toString());
							} else if ("INBOX".equals(entityMap.get("folder_label")))
								MailDistributionPolicy.getInstance().distribute(hsession, entityMap);
						}
					}
				}
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		
		List<String> uids = new ArrayList<String>();
		
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.append("select b.mail_uid from ML_MailBoxView a, ML_MailEntry b where b.id=a.mail_entry_id and a.is_owner=:is_owner and a.operator_id=:operator_id");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("is_owner", Boolean.TRUE);
			query.setParameter("operator_id", LoginSession.currentPrincipalId());
			List<String> list = (List<String>)query.list();
			if(list.isEmpty())
				return null;
			else {
				for (Iterator<String> iterator = list.iterator(); iterator.hasNext();) {
					String mail_uid = iterator.next();
					uids.add(mail_uid);
				}
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		return uids;
	}
	
	private Boolean is_new_contact = false;
	/**
	 * 从客户端向服务端同步邮件信息
	 * @param mail 当前用户未同步的邮件信息
	 */
	@SuppressWarnings("unchecked")
	public void syncUserMail(Map<String, Object> mail)throws Exception{
		mail.remove("id");
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			Long principal_id = LoginSession.currentPrincipalId();
			StringBuilder sb = new StringBuilder();
			Map<String,Object> entityMap = new HashMap<String, Object>();
			boolean isUpdate = false;
			if(mail.containsKey("mail_uid") && !isNull(mail.get("mail_uid"))){
				//先查找在邮件视图中对应的邮件视图 判断message_id是否存在 存在 说明是处理人员在处理
				sb.setLength(0);
				sb.append("select b.id,b.mail_account_id,b.mail_account_label from ML_MailBoxView a, ML_MailEntry b where b.id=a.mail_entry_id and a.is_owner=:is_owner and a.operator_id=:operator_id and b.mail_uid=:mail_uid");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("is_owner", Boolean.TRUE);
				query.setParameter("operator_id", principal_id);
				query.setParameter("mail_uid", mail.get("mail_uid"));
				List<Object> ms = query.list();
				if(!ms.isEmpty()){
					isUpdate = true;
					Object[] record = (Object[])ms.get(0);
					entityMap.put("id", record[0]);
					entityMap.put("mail_account_id", record[1]);
					entityMap.put("mail_account_label", record[2]);
				}else{
					Long mail_account_id = null;
					if(mail.containsKey("mail_account") && !isNull(mail.get("mail_account"))){
						sb.setLength(0);
						sb.append("select id, name from ML_MailAccount where account=:account");
						query = hsession.createQuery(sb.toString());
						query.setParameter("account", mail.get("mail_account"));
						List<Object> list = query.list();
						if(list.isEmpty()){
							if("SENDED".equals(mail.get("folder")) && !isNull(mail.get("operator_id"))){
								sb.setLength(0);
								sb.append("select id, name from ML_MailAccount where account=:account");
								query = hsession.createQuery(sb.toString());
								query.setParameter("account", mail.get("mail_account"));
								list = query.list();
								if(list.isEmpty())
									throw new Exception("邮件账号不存在");
							}else if("DSBOX".equals(mail.get("folder")) || "OUTBOX".equals(mail.get("folder")) && !isNull(mail.get("operator_id"))){
								return;
							}else 
								throw new Exception("邮件账号不存在");
						}
						Object[] record = (Object[])list.get(0);
						mail_account_id = (Long)record[0];
						String account_name = (String)record[1];
						entityMap.put("mail_account_id", mail_account_id);
						entityMap.put("mail_account_label", account_name);
					}
					sb.setLength(0);
					sb.append("select id, mail_account_id from ML_MailEntry where mail_uid=:mail_uid");
					query = hsession.createQuery(sb.toString());
					query.setParameter("mail_uid", mail.get("mail_uid"));
					List list = query.list();
					if(list.isEmpty()){
						isUpdate = false;
					}else{
						Long id = null;
						if(list.size() > 1){
							Iterator<Object> it = list.iterator();
							while (it.hasNext()) {
								Object[] record = (Object[]) it.next();
								if(record[1] == null){
									continue;
								}else if(((Long)record[1]).equals(mail_account_id)){
									id = (Long)record[1];
									break;
								}
							}
						}
						if(id == null)
							entityMap.put("id", ((Object[])list.get(0))[0]);
						else
							entityMap.put("id", id);
						isUpdate = true;
					}
				}
			}else
				throw new Exception("信息不准确");
			
			Map<String, Property> propertys = getEntityPropertys("ML_MailEntry");
			Iterator<Map.Entry<String, Property>> it = propertys.entrySet().iterator();
			while (it.hasNext()) {
				Map.Entry<String, Property> entry = it.next();
				Property p = entry.getValue();
				if("long".equals(p.type)){
					Object value = mail.get(entry.getKey());
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey(), Double.valueOf(value.toString()).longValue());
				}else if("boolean".equals(p.type)){
					Object value = mail.get(entry.getKey());
					if(isNull(value))
						continue;
					boolean b = false;
					if("true".equals(value.toString()))
						b = true;
					else if("false".equals(value.toString()))
						b = false;
					else if("0".equals(value.toString()))
						b = false;
					else 
						b = true;
					
					entityMap.put(entry.getKey(), b);
				}else if("relation".equals(p.type)){
					Object value = mail.get(entry.getKey() + "_id");
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey() + "_id", Double.valueOf(value.toString()).longValue());
					
					value = mail.get(entry.getKey() + "_label");
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey() + "_label", value);
				}else if("principal".equals(p.type)){
					Object value = mail.get(entry.getKey() + "_id");
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey() + "_id", Double.valueOf(value.toString()).longValue());
					
					value = mail.get(entry.getKey() + "_name");
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey() + "_name", value);
				}else if("string".equals(p.type)){
					Object value = mail.get(entry.getKey());
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey(), value.toString());
				}else {
					Object value = mail.get(entry.getKey());
					if(isNull(value))
						continue;
					entityMap.put(entry.getKey(), value);
				}
			}
			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				is_new_contact = false;//是否新联系人
				Map<String, Object> mlContactMap = null;
				if(!entityMap.containsKey("customer_id")){
					//确定联系人信息
					mlContactMap = findMLContact(mail, hsession);
					entityMap.put("customer_id", mlContactMap.get("id"));
					entityMap.put("customer_label", mlContactMap.get("name"));
				}else{
					Long mail_type = (Long)entityMap.get("mail_type");
					if(mail_type == 1 && "SENDED".equals(mail.get("folder"))){
						sb.setLength(0);
						sb.append("update ML_Contact set last_contact_time=:last_contact_time where id=:id");
						Query query = hsession.createQuery(sb.toString());
						query.setParameter("last_contact_time", new Date());
						query.setParameter("id", (Long)entityMap.get("customer_id"));
						query.executeUpdate();
					}
				}
				
				if(mail.containsKey("customer_grade")){
					Long customer_grade = 0L;
					if(!isNull(mail.get("customer_grade")))
						customer_grade = Double.valueOf(mail.get("customer_grade").toString()).longValue();
					if(!customer_grade.equals((Long)mlContactMap.get("grade"))){
						sb.setLength(0);
						sb.append("update ML_Contact set grade=:grade where id=:id");
						Query query = hsession.createQuery(sb.toString());
						query.setParameter("grade", customer_grade);
						query.setParameter("id", mlContactMap.get("id"));
						query.executeUpdate();
					}
				}
				
				if(mail.containsKey("folder") && !isNull(mail.get("folder"))){
					String folder = mail.get("folder").toString();
					sb.setLength(0);
					sb.append("from ML_MailFolder where name=:name");
					Query query = hsession.createQuery(sb.toString());
					query.setParameter("name", folder);
					List<Object> list = query.list();
					if(list.isEmpty()){
						Map<String, Object> mailBoxMap = findMLMailBox((Long)entityMap.get("mail_account_id"), hsession);
						Map<String,String> folderMap = new HashMap<String, String>();
						folderMap.put("name", folder);
						folderMap.put("mail_box_id", getValue(mailBoxMap.get("id")));
						folderMap.put("mail_box_label", getValue(mailBoxMap.get("name")));
						Map<String, Object> folderEntity = DynamicEntityService.createEntity("ML_MailFolder", folderMap, null);
						entityMap.put("folder_id", folderEntity.get("id"));
						entityMap.put("folder_label", folderEntity.get("name"));
					}else{
						Map<String, Object> folderEntity = (Map<String, Object>)list.get(0);
						entityMap.put("folder_id", folderEntity.get("id"));
						entityMap.put("folder_label", folderEntity.get("name"));
					}
				}
				
				if(isUpdate){
					hsession.merge("ML_MailEntry", entityMap);
					//更改邮件对应邮件视图中的 is_seen is_handled
					if(entityMap.containsKey("is_seen") || entityMap.containsKey("is_handled")){
						sb.setLength(0);
						sb.append("update ML_MailBoxView set is_seen=:is_seen,seen_time=:seen_time,is_handled=:is_handled,handle_time=:handle_time ");
						sb.append(" where mail_entry_id=:mail_entry_id and is_owner=:is_owner and operator_id=:operator_id");
						Query query = hsession.createQuery(sb.toString());
						query.setParameter("mail_entry_id", entityMap.get("id"));
						query.setParameter("is_owner", Boolean.TRUE);
						query.setParameter("operator_id", principal_id);
						if(((Boolean)entityMap.get("is_seen") == null) ?false : (Boolean)entityMap.get("is_seen")){
							query.setParameter("is_seen", Boolean.TRUE);
							query.setParameter("seen_time", new Date());
						}else{
							query.setParameter("is_seen", Boolean.FALSE);
							query.setParameter("seen_time", null);
						}
						if(((Boolean)entityMap.get("is_handled") == null) ?false : (Boolean)entityMap.get("is_handled")){
							query.setParameter("is_handled", Boolean.TRUE);
							query.setParameter("handle_time", new Date());
						}else{
							query.setParameter("is_handled", Boolean.FALSE);
							query.setParameter("handle_time", null);
						}
						query.executeUpdate();
					}
				}
				else{
					hsession.persist("ML_MailEntry", entityMap);
					entityMap.put("is_new_contact", is_new_contact);
					
					Long mail_type = (Long)entityMap.get("mail_type");
					if(mail_type != MailEntry.DRAFT_MAIL){
						String folder = getValue(mail.get("folder"));
						//如果是审核邮件 不进行分发邮件。邮件视图包括自身和目标人员
						if("DSBOX".equals(folder)){
							Map<String,String> _entityMap = new HashMap<String, String>();
							_entityMap.put("operator_id", getValue(mail.get("owner_user_id")));
							_entityMap.put("mail_entry_id", getValue(entityMap.get("id")));
							_entityMap.put("mail_entry_label", getValue(entityMap.get("subject")));
							_entityMap.put("recv_time", getValue(new Date().getTime()));
							_entityMap.put("is_owner", getValue(Boolean.TRUE));
							_entityMap.put("is_new_contact", getValue(is_new_contact));
							
							DynamicEntityService.createEntity("ML_MailBoxView", _entityMap, null);
							
							_entityMap = new HashMap<String, String>();
							_entityMap.put("operator_id", getValue(mail.get("reviewer_id")));
							_entityMap.put("operator_name", getValue(mail.get("reviewer_name")));
							_entityMap.put("mail_entry_id", getValue(entityMap.get("id")));
							_entityMap.put("mail_entry_label", getValue(entityMap.get("subject")));
							_entityMap.put("recv_time", getValue(new Date().getTime()));
							_entityMap.put("is_owner", getValue(Boolean.TRUE));
							_entityMap.put("is_new_contact", getValue(is_new_contact));
							DynamicEntityService.createEntity("ML_MailBoxView", _entityMap, null);
							
							//新待审邮件通知
							Principal from = PrincipalService.getPrincipal(Double.valueOf(mail.get("owner_user_id").toString()).longValue());
							Principal to = PrincipalService.getPrincipal((Long)entityMap.get("reviewer_id"));
							sb.setLength(0);
							sb.append("<message type=\"MailDsBoxDeliver\" >")
							  .append("<subject>").append(from.getName()).append("向您转交了一个新待审邮件通知").append("</subject>")
							  .append("<body>")
								  .append("<message>").append(from.getName()).append("向您转交了一个新待审邮件通知").append("</message>")
							  .append("</body>")
							  .append("<icon></icon>")
							  .append("</message>");
							AddMessage(to.getLoginId() + "@" + to.getDomainName(), sb.toString());
						}else if("INBOX".equals(entityMap.get("folder_label")))
							MailDistributionPolicy.getInstance().distribute(hsession, entityMap);
					}
				}
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	private Map<String, Property> getEntityPropertys(String entityName){
		DynamicEntity dynamicEntity = EntityManager.instance().getEntity(entityName);
		return dynamicEntity.getPropertyMap();
	}
	
	/**
	 * 转换 把Object为Double或Int的值转换成Long
	 * @param account
	 * @throws Exception 
	 */
	private Map<String, Object> convertMap(Map<String, Object> account, String entityName) throws Exception{
		DynamicEntity dynamicEntity = ime.core.dynamic.EntityManager.instance().getEntity(entityName);
		if (dynamicEntity == null)
			throw new Exception("找不到指定的实体对象定义：" + entityName);
		Map<String, Object> entityMap = new HashMap<String, Object>();
		Iterator<Map.Entry<String, Property>> it = dynamicEntity.getPropertyMap().entrySet().iterator();
		while (it.hasNext()) {
			Map.Entry<String, Property> entry = it.next();
			String propName = entry.getKey();
			DynamicEntity.Property prop = entry.getValue();
			if("relation".equals(prop.type)){
				if(account.containsKey(propName + "_id")){
					entityMap.put(propName + "_id", NumberUtil.toLong(account.get(propName + "_id").toString().trim()));
					entityMap.put(propName + "_label", account.get(propName + "_label").toString().trim());
				}
			}else if("principal".equals(prop.type)){
				if(account.containsKey(propName + "_id")){
					entityMap.put(propName + "_id", NumberUtil.toLong(account.get(propName + "_id").toString().trim()));
					entityMap.put(propName + "_name", account.get(propName + "_name").toString().trim());
				}
			}else if(account.containsKey(propName)){
				Object value = account.get(propName);
				if (value == null || "string".equals(prop.type))
					entityMap.put(propName, value);
				else
					entityMap.put(propName, convertValue(value.toString(), prop.type));
			}
		}
		return entityMap;
		/*
		Iterator<Map.Entry<String, Object>> it = account.entrySet().iterator();
		while (it.hasNext()) {
			Map.Entry<String, Object> entry = it.next();
			String propName = entry.getKey();
			DynamicEntity.Property prop = dynamicEntity.getPropertyDefinition(propName);
			if(prop == null){
				it.remove();
				continue;
			}
			String typeName = prop.type;
			Object value = entry.getValue();
			if (value == null || typeName.equals("string"))
				account.put(propName, value);
			else
				account.put(propName, convertValue(value.toString(), typeName));
		}*/
	}
	
	/**
	 * 将以字符串表示的值转换为实际对象类型
	 * @param value 字符串形式的值
	 * @param typeName 目标类型名称
	 * @return 转换后的值对象
	 */
	public Object convertValue(String value, String typeName){
		if( "boolean".equals(typeName) ){
			if( value.equals("true") || value.equals("1") )
				return Boolean.TRUE;
			else
				return Boolean.FALSE;
        }
		else if( "byte".equals(typeName) ){
            if (value.length() == 0){
                byte b = 0;
                return new Byte(b);
            }
            return NumberUtil.toByte(value.trim());
        }
		else if( "short".equals(typeName) ){
            if (value.length() == 0){
                short s = 0;
                return new Short(s);
            }
            return NumberUtil.toShort(value.trim());
        }
		else if( "integer".equals(typeName) ){
            if (value.length() == 0){
                return new Integer(0);
            }
            return NumberUtil.toInt(value.trim());
        }
		else if( "long".equals(typeName) ){
            if (value.length() == 0){
                return new Long(0);
            }
            return NumberUtil.toLong(value.trim());
        }
		else if( "float".equals(typeName) ){
            if (value.length() == 0){
                return new Float(0);
            }
            return NumberUtil.toFloat(value.trim());
        }
		else if( "double".equals(typeName) ){
            if (value.length() == 0){
                return new Double(0);
            }
            return new Double(value.trim());
        }
		else if( "timestamp".equals(typeName) || "time".equals(typeName) || "date".equals(typeName) ){
			long tm = NumberUtil.toLong(value.trim());
			Date date = new Date();
			date.setTime(tm);
			return date;
		}
        
        return value;
	}
	
	@SuppressWarnings("unchecked")
	private Map<String, Object> findMLContact(Map<String, Object> mail, Session session) throws Exception{
		StringBuilder sb = new StringBuilder();
		sb.append("from ML_Contact where email=:email");
		Query query = session.createQuery(sb.toString());
		query.setParameter("email", mail.get("contact_mail"));
		List<Object> list = query.list();
		if(list.isEmpty()){
			is_new_contact = true;
			Map<String,String> entityMap = new HashMap<String, String>();
			entityMap.put("email", getValue(mail.get("contact_mail")));
			entityMap.put("name", getValue(mail.get("mail_from_label")));
			entityMap.put("last_contact_time", getValue(new Date().getTime()));
			return DynamicEntityService.createEntity("ML_Contact", entityMap, null);
		}
		return (Map<String, Object>)list.get(0);
	}
	
	@SuppressWarnings("unchecked")
	private Map<String, Object> findMLMailBox(Long mail_account_id, Session session) throws Exception{
		StringBuilder sb = new StringBuilder();
		sb.append("from ML_MailBox where mail_account_id=:mail_account_id");
		Query query = session.createQuery(sb.toString());
		query.setParameter("mail_account_id", mail_account_id);
		List<Object> list = query.list();
		if(list.isEmpty()){
			return null;
		}
		return (Map<String, Object>)list.get(0);
	}
	
	private String getValue(Object val) {
		if (val == null)
			return null;
		return String.valueOf(val);
	}
	
	private Boolean isNull(Object val) {
		if(val == null || "null".equals(val.toString()) || "".equals(val.toString().trim()))
			return true;
		else
			return false;
	}
	
	/**
	 * 将发件人（域）添加到黑名单
	 * @param email
	 * @param email_domain
	 * @param display_name
	 * @throws Exception
	 */
	public void addBlackList(String email, String email_domain, String display_name) throws Exception{
		Long black_level = -1L;
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			if(!isNull(email) && isNull(email_domain)){
				sb.setLength(0);
				sb.append("select id from ML_BlackList where email=:email");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("email", email);
				if(!query.list().isEmpty())
					return;
				
				black_level = 0L;
			}else if(isNull(email) && !isNull(email_domain)){
				sb.setLength(0);
				sb.append("select id from ML_BlackList where email_domain=:email_domain");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("email_domain", email_domain);
				if(!query.list().isEmpty())
					return;
				black_level = 1L;
			}else
				return;
			
			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				
				Map<String, String> entityMap = new HashMap<String, String>();
				if(black_level == 0)
					entityMap.put("email", email);
				else if(black_level == 1)
					entityMap.put("email_domain", email_domain);
				entityMap.put("black_level", getValue(black_level));
				entityMap.put("display_name", display_name);
				DynamicEntityService.createEntity("ML_BlackList", entityMap, null);
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 将发件人（域）从白名单中移除
	 * @param email
	 * @param email_domain
	 * @throws Exception
	 */
	@SuppressWarnings("unchecked")
	public void removeBlackList(String email, String email_domain) throws Exception{
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			Long blackList_id = null;
			List<Object> list = null;
			if(!isNull(email) && isNull(email_domain)){
				sb.setLength(0);
				sb.append("select id from ML_BlackList where email=:email");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("email", email);
				list = query.list();
				if(!list.isEmpty()){
					blackList_id = (Long)list.get(0);
				}
			}else if(isNull(email) && !isNull(email_domain)){
				sb.setLength(0);
				sb.append("select id from ML_BlackList where email_domain=:email_domain");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("email_domain", email_domain);
				list = query.list();
				if(!list.isEmpty()){
					blackList_id = (Long)list.get(0);
				}
			}else
				return;
			
			if(blackList_id == null)
				return;
			String hql = "delete ML_BlackList where id=:id";
			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();
				
				
				Query query = hsession.createQuery(hql);
				query.setParameter("id", blackList_id);
				query.executeUpdate();
				
				if (owner_tx)
					tx.commit();
			} catch (Exception e) {
				if (owner_tx)
					tx.rollback();
				throw e;
			}
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 获取白名单
	 * @return
	 * @throws Exception
	 */
	@SuppressWarnings("unchecked")
	public Map<String, List<String>> getBlackList() throws Exception{
		Map<String, List<String>> map = new HashMap<String, List<String>>();
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.append("select email,email_domain,black_level from ML_BlackList");
			Query query = hsession.createQuery(sb.toString());
			List<Object> list = query.list();
			Iterator<Object> it = list.iterator();
			while (it.hasNext()) {
				Object[] record = (Object[]) it.next();
				
				Long black_level = (Long)record[2];
				if(black_level == null){
					continue;
				}else if(black_level == 0){
					if(map.containsKey("email")){
						List<String> emails = map.get("email");
						emails.add((String)record[0]);
					}else{
						List<String> emails = new ArrayList<String>();
						emails.add((String)record[0]);
						map.put("email", emails);
					}
				}else if(black_level == 1){
					if(map.containsKey("email_domain")){
						List<String> email_domains = map.get("email_domain");
						email_domains.add((String)record[1]);
					}else{
						List<String> email_domains = new ArrayList<String>();
						email_domains.add((String)record[1]);
						map.put("email_domain", email_domains);
					}
				}
			}
			
			return map;
		} catch (Exception e) {
			logger.error(e.getMessage(), e);
			throw e;
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}
	
	/**
	 * 获取用户的邮件视图
	 * @return
	 */
	public Map<String, Map<String,Object>> getMailBoxView(){
		Long owner_user_id = null;
		try {
			owner_user_id = LoginSession.currentPrincipalId();
			return getMailBoxView(owner_user_id);
		} catch (Exception e) {
			e.printStackTrace();
		}
		
		return null;
	}
	
	/**
	 * 获取用户的邮件视图
	 * @param owner_user_id
	 * @return
	 */
	@SuppressWarnings("unchecked")
	public Map<String, Map<String,Object>> getMailBoxView(Long owner_user_id){
		Map<String, Map<String,Object>> result = new HashMap<String, Map<String,Object>>();
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.append("select a, b, c.account from ML_MailBoxView a, ML_MailEntry b, ML_MailAccount c where c.id=b.mail_account_id and b.id=a.mail_entry_id and ")
				.append("a.operator_id=:operator_id and a.is_owner=:is_owner");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("operator_id", owner_user_id);
			query.setParameter("is_owner", Boolean.TRUE);
			List list = query.list();
			Iterator it = list.iterator();
			while (it.hasNext()) {
				Object[] record = (Object[]) it.next();       
				Map<String, Object> mailEntry = (Map<String, Object>)record[1];
				Map<String,Object> map = new HashMap<String, Object>();
				Iterator<Map.Entry<String, Object>> itEntry = mailEntry.entrySet().iterator();
				while (itEntry.hasNext()) {
					Map.Entry<String, Object> _entry = itEntry.next();
					map.put(_entry.getKey(), _entry.getValue());
				}
				map.put("owner_user_id", owner_user_id);
				map.put("folder", map.get("folder_label"));
				map.put("mail_account", (String)record[2]);
				if(mailEntry.containsKey("mail_uid")){
					if(mailEntry.get("mail_uid") == null || "".equals((String)mailEntry.get("mail_uid")))
						continue;
					result.put((String)mailEntry.get("mail_uid"), map);
				}
			}
		} catch (Exception e) {
			e.printStackTrace();
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		return result;
	}
	
	/**
	 * 获取被移动的邮件uid
	 * @param owner_user_id
	 * @return
	 */
	@SuppressWarnings("unchecked")
	public List<String> getMoveMailBoxView(Long owner_user_id){
		List<String> result = new ArrayList<String>();
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			
			sb.append("select a1.mail_uid from (select distinct b.mail_uid from DYNA_ML_MailBoxView a, DYNA_ML_MailEntry b where b.id=a.mail_entry_id and a.operator_id=:operator_id and a.is_owner=:is_owner0) a1")
				.append(" where not exists(select * from (select b0.mail_uid from DYNA_ML_MailBoxView a0, DYNA_ML_MailEntry b0 where b0.id=a0.mail_entry_id and a0.operator_id=:operator_id and a0.is_owner=:is_owner1) b1 where b1.mail_uid=a1.mail_uid)");
			Query query = hsession.createSQLQuery(sb.toString());
			query.setParameter("operator_id", owner_user_id);
			query.setParameter("is_owner0", Boolean.FALSE);
			query.setParameter("is_owner1", Boolean.TRUE);
			List<Object> list = query.list();
			Iterator<Object> it = list.iterator();
			while (it.hasNext()) {
				String uid = (String) it.next();       
				result.add(uid);
			}
		} catch (Exception e) {
			e.printStackTrace();
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		return result;
	}
	
	/**
	 * 获取国家简称
	 * @param ip_from
	 * @return
	 */
	@SuppressWarnings("unchecked")
	public String getCountrysName(Object ip_from){
		if(!(ip_from instanceof Double))
			return "";
		SessionContext ctx = null;
		try {
			Long ip_from_v = ((Double)ip_from).longValue();
			
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.append("select country_sname from ML_IPLocation where ip_from<=:ip_from and ip_to>=:ip_to");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("ip_from", ip_from_v);
			query.setParameter("ip_to", ip_from_v);
			List list = query.list();
			if(list.isEmpty())
				return "";
			
			return (String)list.get(0);
		} catch (Exception e) {
			e.printStackTrace();
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		return null;
	}
	
	/**
	 * 搜索邮件
	 * @param folder
	 * @param labelField
	 * @param search
	 * @return
	 */
	@SuppressWarnings("unchecked")
	public List<Map<String,Object>> getSearchMails(String folder, String labelField, String search){
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
            sb.append("from ML_MailEntry where folder_label = :folder ")
               .append("and ").append(labelField).append(" like '%").append(search).append("%'");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("folder", folder);
			List list = query.list();
			if(list.isEmpty())
				return null;
			
			return (List<Map<String,Object>>)list;
		} catch (Exception e) {
			e.printStackTrace();
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
		
		return null;
	}

	public static Principal getPrincipal(Object principalId) throws Exception{
		if(principalId == null)
			return null;
		return PrincipalService.getPrincipal(Double.valueOf(principalId.toString()).longValue());
	}
	
	private static Map<String, List<String>> messageMap = new HashMap<String, List<String>>();
	private static Map<String, List<String>> moveMessageMap = new HashMap<String, List<String>>();
	private boolean isStart = false;
	private boolean isMoveStart = false;
	public void AddMessage(String p, String message){
		if(messageMap.containsKey(p)){
			List<String> list = messageMap.get(p);
			list.add(message);
		}else{
			List<String> list = new ArrayList<String>();
			list.add(message);
			messageMap.put(p, list);
		}
		
		if(!isStart){
			new MailMessageThread().start();
			isStart = true;
		}
	}
	
	public void AddMoveMessage(String p, String message){
		if(moveMessageMap.containsKey(p)){
			List<String> list = moveMessageMap.get(p);
			list.add(message);
		}else{
			List<String> list = new ArrayList<String>();
			list.add(message);
			moveMessageMap.put(p, list);
		}
		
		if(!isMoveStart){
			new MailMoveMessageThread().start();
			isMoveStart = true;
		}
	}
	
	class MailMessageThread extends Thread {
		long SLEEPTIME = 1000 * 20;// 20秒处理一次

		@Override
		public void run() {
			while (true) {
				try {
					sleep(SLEEPTIME);
				} catch (InterruptedException e) {
					e.printStackTrace();
				}
				
				Iterator<Map.Entry<String, List<String>>> it = messageMap.entrySet().iterator();
				while (it.hasNext()) {
					Map.Entry<String, List<String>> entry = it.next();
					if(entry.getValue().isEmpty()){
						continue;
					}
					List<String> messages = entry.getValue();
					
					if(messages.size() < 3){
						for (String string : messages) {
							VysperServer.instance().sendMessage("messenger@ime.com", entry.getKey(), string);
						}
					}else{
						VysperServer.instance().sendMessage("messenger@ime.com", entry.getKey(), messages.get(0));
					}
					
					messages.clear();
				}
			}
		}
	}
	
	class MailMoveMessageThread extends Thread {
		long SLEEPTIME = 1000 * 20;// 20秒处理一次

		@Override
		public void run() {
			while (true) {
				try {
					sleep(SLEEPTIME);
				} catch (InterruptedException e) {
					e.printStackTrace();
				}
				
				Iterator<Map.Entry<String, List<String>>> it = moveMessageMap.entrySet().iterator();
				while (it.hasNext()) {
					Map.Entry<String, List<String>> entry = it.next();
					if(entry.getValue().isEmpty()){
						continue;
					}
					List<String> messages = entry.getValue();
					
					if(messages.size() < 3){
						for (String string : messages) {
							VysperServer.instance().sendMessage("messenger@ime.com", entry.getKey(), string);
						}
					}else{
						VysperServer.instance().sendMessage("messenger@ime.com", entry.getKey(), messages.get(0));
					}
					
					messages.clear();
				}
			}
		}
	}
}