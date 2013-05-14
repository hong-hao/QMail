package ime.mail;

import ime.core.services.DynamicEntityService;
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
import java.util.Random;

import org.hibernate.Query;
import org.hibernate.Session;
import org.hibernate.Transaction;
import org.json.JSONArray;
import org.json.JSONObject;

import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

/**
 * ÿ���ʼ��ʺŵķ���������ݶ���Ϊ
 * {
 * 		managers : [{id=xxx, name:"", loginId:"xxx@xxx.xxx"}],		//������Ա
 * 		distributor : [{id=xxx, name:"", loginId:"xxx@xxx.xxx"}],	//������Ա
 * 		is_hide_email : true|false, //�Ƿ������ʼ���ַ
 * 		distribution_mode : "dynamic" | "static", //Ĭ�Ϸ��䷽ʽ����̬��̬
 * 		new_customer_policy : {	//���û��ʼ���̬�������
 * 			online_unread_timeout : 30, //����״̬�£��¿ͻ��ʼ�δ��ת�Ƶĳ�ʱʱ��(��λ����)
 * 			unhandled_timeout : 120, //���Ѳ鿴�ʼ���δ��ʱ�����ת�Ƶĳ�ʱʱ��(��λ����)
 * 			offline_timeout : 45,	//�����������û����߶���ת���ʼ��ĳ�ʱʱ��(��λ����)
 * 		}
 * 		old_customer_policy :{	//���û��ʼ���̬�������
 * 			unread_timeout : 24, 	//���û���δ���ʼ�ת�Ƶĳ�ʱʱ��(��λ��Сʱ)
 * 			unhandled_timeout : 12, //���û���δ�����ʼ�ת�Ƶĳ�ʱʱ��(��λ��Сʱ)
 * 			uncontact_timeout : 100, //���û���δ��ϵ��ת�Ƹ�����Ա�ĳ�ʱʱ��(��λ����)
 * 		}
 * }
 * @author honghao
 *
 */
public class MailDistributionPolicy {
	
	private static Map<Long, JSONObject> policyMap = new HashMap<Long, JSONObject>();
	private static Map<Long, Long> lastDistUserMap = new HashMap<Long, Long>();
	private Random r = new Random();
	
	private static MailDistributionPolicy instance;
	
	public static MailDistributionPolicy getInstance(){
		if( instance == null ){
			instance = new MailDistributionPolicy();
		}
		
		return instance;
	}
	
	/**
	 * ��̬�����������
	 */
	public void dynamicPolicyStart(){
		new DynamicPolicyThread().start();
	}
	
	/**
	 * װ�ز�������
	 */
	@SuppressWarnings("unchecked")
	public void reloadPolicy(){
		policyMap.clear();
		lastDistUserMap.clear();
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();
			Session hsession = ctx.getHibernateSession();
			StringBuilder sb = new StringBuilder();
			sb.setLength(0);
			sb.append("select id, config_data from ML_MailBox");
			Query query = hsession.createQuery(sb.toString());
			List<Object> list = query.list();
			Iterator<Object> it = list.iterator();
			while (it.hasNext()) {
				Object[] mail = (Object[]) it.next();
				try{
					policyMap.put((Long)mail[0], new JSONObject(getValue(mail[1])));
				}catch (Exception e) {
					
				}
			}
		} catch (Exception e) {
			
		} finally {
			EntityManagerUtil.closeSession(ctx);
		}
	}

	/**
	 * �ַ��ʼ�
	 * @param em
	 * @param mailRecord
	 */
	@SuppressWarnings("unchecked")
	public void distribute(Session hsession, Map<String, Object> mailRecord) throws Exception{
		/*
		 * 1. �����ʼ����͵�ַ�������Ӧ�Ĵ�����Ա
		 * 2. ����ҵ�������Ա������ML_MailBoxView��д����ؼ�¼������
		 * 3. ���δ�ҵ�������ʼ��ʺŵķ���Ⱥ�е�������Ա������ҳ�һ��������Ա����д����ML_MailBoxView��
		 * 4. �����ǰ����Ⱥ��û��һ����Ա���ߣ���ӷ���Ⱥ������ҳ�һ��������Ա����д����ML_MailBoxView��
		 */
		StringBuilder sb = new StringBuilder();
		Map<String, Object> mailBoxMap = null;
		Long customer_id = null;
		if(mailRecord.containsKey("customer_id"))
			customer_id = (Long)mailRecord.get("customer_id");
		Long mail_account_id = null;
		if(mailRecord.containsKey("mail_account_id")){
			mail_account_id = (Long)mailRecord.get("mail_account_id");
			
			mailBoxMap = findMLMailBox(mail_account_id, hsession);
		}
		
		if(mailBoxMap == null)
			return;
		
		if(customer_id != null){
			sb.setLength(0);
			sb.append("select dist_user_id, dist_user_name,dist_type from ML_ContactDistribution where contact_id=:contact_id");
			Query query = hsession.createQuery(sb.toString());
			query.setParameter("contact_id", customer_id);
			List<Object> list = query.list();
			if(!list.isEmpty()){
				Object[] record = (Object[])list.get(0);
				Map<String,String> entityMap = new HashMap<String, String>();
				entityMap.put("operator_id", getValue(record[0]));
				entityMap.put("operator_name", getValue(record[1]));
				entityMap.put("mail_entry_id", getValue(mailRecord.get("id")));
				entityMap.put("mail_entry_label", getValue(mailRecord.get("subject")));
				entityMap.put("is_new_contact", getValue(mailRecord.get("is_new_contact")));
				entityMap.put("recv_time", getValue(new Date().getTime()));
				entityMap.put("is_owner", getValue(Boolean.TRUE));
				entityMap.put("mail_box_id", getValue(mailBoxMap.get("id")));
				entityMap.put("mail_box_label", getValue(mailBoxMap.get("name")));
				entityMap.put("is_handled", getValue(Boolean.FALSE));
				entityMap.put("is_seen", getValue(Boolean.FALSE));
				Long dist_type = (Long)record[2];
				if(dist_type == 1)
					entityMap.put("is_static_assign", getValue(Boolean.TRUE));
				else{
					if(mailRecord.containsKey("is_handled") && (Boolean)mailRecord.get("is_handled"))
						return;
					entityMap.put("is_static_assign", getValue(Boolean.FALSE));
				}
				
				DynamicEntityService.createEntity("ML_MailBoxView", entityMap, null);
				/*
				Map<String,Object> _entityMap = new HashMap<String, Object>();
				_entityMap.put("operator_id", Long.valueOf(entityMap.get("operator_id")));
				_entityMap.put("operator_name", entityMap.get("operator_name"));
				_entityMap.put("id", Long.valueOf(entityMap.get("mail_entry_id")));
				hsession.merge("ML_MailEntry", _entityMap);
				*/
				//��̬�����ʼ�֪ͨ
				Principal to = PrincipalService.getPrincipal((Long)record[0]);
				sb.setLength(0);
				sb.append("<message type=\"MailDsBoxDeliver\" >")
				  .append("<subject>").append("�����·�����ʼ�").append("</subject>")
				  .append("<body>")
					  .append("<message>").append("�����·�����ʼ�").append("</message>")
				  .append("</body>")
				  .append("<icon></icon>")
				  .append("</message>");
				MailManager.getInstance().AddMessage(to.getLoginId() + "@" + to.getDomainName(), sb.toString());
				return;
			}
			
			if((mailRecord.containsKey("is_handled") && (Boolean)mailRecord.get("is_handled")))
				return;
		}
		
		if(mail_account_id != null){
			Long mailbox_id = (Long)mailBoxMap.get("id");
			if(!policyMap.containsKey(mailbox_id))
				return;
			try{
				
				Long last_dist_user_id = lastDistUserMap.get(mailbox_id);
				
				JSONObject config_data = policyMap.get(mailbox_id);
				if(config_data != null && config_data.has("distribution_policy")){
					JSONObject distribution_policy = config_data.optJSONObject("distribution_policy");
					if(distribution_policy == null || !distribution_policy.has("distributor"))
						return;
					
					JSONArray distributor = distribution_policy.optJSONArray("distributor");
					JSONObject nextUser = getNextUser(last_dist_user_id, distributor);
					if(nextUser == null)
						return;
					
					String distribution_mode = distribution_policy.optString("distribution_mode");
					
					Map<String,Object> MLContact = findMLContact(getValue(mailRecord.get("contact_mail")), hsession);
					if(MLContact == null)
						return;
					Map<String,Object> cdEntity = null;
					sb.setLength(0);
					sb.append("from ML_ContactDistribution where contact_id=:contact_id");
					Query query = hsession.createQuery(sb.toString());
					query.setParameter("contact_id", MLContact.get("id"));
					List list = query.list();
					if(list.isEmpty()){
						Map<String,String> entityMap = new HashMap<String, String>();
						entityMap.put("contact_id", getValue(MLContact.get("id")));
						entityMap.put("contact_label", getValue(MLContact.get("name")));
						entityMap.put("dist_user_id", getValue(nextUser.get("id")));
						entityMap.put("dist_user_name", getValue(nextUser.get("name")));
						entityMap.put("dist_type", "static".equals(distribution_mode) ? "1" : "2");
						entityMap.put("create_time", getValue(new Date().getTime()));
						
						cdEntity = DynamicEntityService.createEntity("ML_ContactDistribution", entityMap, null);
					}else
						cdEntity = (Map<String,Object>)list.get(0);
					Long dist_type = (Long)cdEntity.get("dist_type");
					
					Map<String,String> entityMap = new HashMap<String, String>();
					entityMap.put("operator_id", getValue(nextUser.get("id")));
					entityMap.put("operator_name", getValue(nextUser.get("name")));
					entityMap.put("mail_entry_id", getValue(mailRecord.get("id")));
					entityMap.put("mail_entry_label", getValue(mailRecord.get("subject")));
					entityMap.put("is_new_contact", getValue(mailRecord.get("is_new_contact")));
					entityMap.put("recv_time", getValue(new Date().getTime()));
					entityMap.put("is_owner", getValue(Boolean.TRUE));
					entityMap.put("mail_box_id", getValue(mailBoxMap.get("id")));
					entityMap.put("mail_box_label", getValue(mailBoxMap.get("name")));
					entityMap.put("is_handled", getValue(Boolean.FALSE));
					entityMap.put("is_seen", getValue(Boolean.FALSE));
					if(dist_type == 1)
						entityMap.put("is_static_assign", getValue(Boolean.TRUE));
					else
						entityMap.put("is_static_assign", getValue(Boolean.FALSE));
					
					DynamicEntityService.createEntity("ML_MailBoxView", entityMap, null);
					/*
					Map<String,Object> _entityMap = new HashMap<String, Object>();
					_entityMap.put("operator_id", Long.valueOf(entityMap.get("operator_id")));
					_entityMap.put("operator_name", entityMap.get("operator_name"));
					_entityMap.put("id", Long.valueOf(entityMap.get("mail_entry_id")));
					hsession.merge("ML_MailEntry", _entityMap);
					*/
					//��̬�����ʼ�֪ͨ
					Principal to = PrincipalService.getPrincipal(nextUser.optLong("id"));
					sb.setLength(0);
					sb.append("<message type=\"MailDsBoxDeliver\" >")
					  .append("<subject>").append("�����·�����ʼ�").append("</subject>")
					  .append("<body>")
						  .append("<message>").append("�����·�����ʼ�").append("</message>")
					  .append("</body>")
					  .append("<icon></icon>")
					  .append("</message>");
					MailManager.getInstance().AddMessage(to.getLoginId() + "@" + to.getDomainName(), sb.toString());
					
					lastDistUserMap.put(mailbox_id, Double.valueOf(getValue(nextUser.get("id"))).longValue());
				}
				
			}catch (Exception e) {
				e.printStackTrace();
			}
		}
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
	
	/**
	 * ��ȡ�¸������û�
	 * @param last_dist_user_id
	 * @param distributor
	 * @return
	 * @throws Exception
	 */
	private JSONObject getNextUser(Long last_dist_user_id, JSONArray distributor) {
		List<JSONObject> lines = new ArrayList<JSONObject>();
		if (distributor.length() == 0)
			return null;
		for (int i = 0; i < distributor.length(); i++) {
			JSONObject user = distributor.optJSONObject(i);
			if (VysperServer.instance().isUserOnline(user.optString("loginId")))
				lines.add(user);
		}
		int index = -1;

		if (!lines.isEmpty()) {
			for (int i = 0; i < lines.size(); i++) {
				JSONObject user = lines.get(i);
				Long user_id = user.optLong("id");
				if (user_id.equals(last_dist_user_id)) {
					index = i;
					break;
				}
			}

			if (index == -1)
				return lines.get(r.nextInt(lines.size()));

			if (index + 1 < lines.size())
				return lines.get(index + 1);
			else
				return lines.get(r.nextInt(lines.size()));
		} else {
			for (int i = 0; i < distributor.length(); i++) {
				JSONObject user = distributor.optJSONObject(i);
				Long user_id = user.optLong("id");
				if (user_id.equals(last_dist_user_id)) {
					index = i;
					break;
				}
			}

			if (index == -1)
				return distributor.optJSONObject(r.nextInt(distributor.length()));

			if (index + 1 < distributor.length())
				return distributor.optJSONObject(index + 1);
			else
				return distributor.optJSONObject(r.nextInt(distributor.length()));
		}
	}
	
	private String getValue(Object val) {
		if (val == null)
			return null;
		return String.valueOf(val);
	}
	
	@SuppressWarnings("unchecked")
	private Map<String, Object> findMLContact(String contact_mail, Session session) throws Exception{
		if(contact_mail == null)
			return null;
		StringBuilder sb = new StringBuilder();
		sb.append("from ML_Contact where email=:email");
		Query query = session.createQuery(sb.toString());
		query.setParameter("email", contact_mail);
		List<Object> list = query.list();
		if(list.isEmpty())
			return null;
		return (Map<String, Object>)list.get(0);
	}
	
	class DynamicPolicyThread extends Thread{
		List<Map<String,Object>> oldBoxViews = new ArrayList<Map<String,Object>>();
		List<Map<String,Object>> newBoxViews = new ArrayList<Map<String,Object>>();
		long SLEEPTIME = 1000 * 60 * 5;//5���Ӵ���һ��
		
		@Override
		public void run() {
			System.out.println("----�����������----");
			while(true){
				
				try {
					sleep(SLEEPTIME);
				} catch (InterruptedException e) {
					e.printStackTrace();
				}
				System.out.println("----������Կ�ʼ----");
				Reset();
				
				newContactMailHandle();
				oldContactMailHandle();
				System.out.println("----������Խ���----");
			}
		}
		
		@SuppressWarnings("unchecked")
		private void Reset(){
			oldBoxViews.clear();
			newBoxViews.clear();
			
			SessionContext ctx = null;
			try {
				ctx = EntityManagerUtil.currentContext();
				Session hsession = ctx.getHibernateSession();
				StringBuilder sb = new StringBuilder();
				sb.append("select a,b.id from ML_MailBoxView a, ML_MailEntry b where b.id=a.mail_entry_id")
					.append(" and a.is_static_assign=:is_static_assign and a.is_owner=:is_owner and a.is_handled=:is_handled")
					.append(" and b.folder_label=:folder");
				Query query = hsession.createQuery(sb.toString());
				query.setParameter("is_static_assign", Boolean.FALSE);
				query.setParameter("is_owner", Boolean.TRUE);
				query.setParameter("is_handled", Boolean.FALSE);
				query.setParameter("folder", "INBOX");
				List<Object> list = query.list();
				Iterator<Object> it = list.iterator();
				while (it.hasNext()) {
					Object[] record = (Object[])it.next();
					Map<String,Object> entry = (Map<String,Object>) record[0];
					Map<String,Object> map = new HashMap<String, Object>();
					Iterator<Map.Entry<String, Object>> itEntry = entry.entrySet().iterator();
					while (itEntry.hasNext()) {
						Map.Entry<String, Object> _entry = itEntry.next();
						map.put(_entry.getKey(), _entry.getValue());
					}
					if((Boolean)entry.get("is_new_contact"))
						newBoxViews.add(map);
					else
						oldBoxViews.add(map);
				}
			} catch (Exception e) {
				e.printStackTrace();
			} finally {
				EntityManagerUtil.closeSession(ctx);
			}
		}
		
		/**
		 * �����¿ͻ���¼
		 */
		private void newContactMailHandle(){
			//�¿ͻ��ʼ�����
			Iterator<Map<String,Object>> it = newBoxViews.iterator();
			while (it.hasNext()) {
				Map<String, Object> entry = it.next();
				try {
					Boolean is_seen = entry.get("is_seen") == null ? false : (Boolean)entry.get("is_seen");
					Boolean is_handled = entry.get("is_handled") == null ? false : (Boolean)entry.get("is_handled");
					Date recv_time = (Date)entry.get("recv_time");
					//��ȡ�������
					Long mailbox_id = (Long)entry.get("mail_box_id");
					JSONArray distributor = getDistributor(mailbox_id);
					if(distributor == null)
						continue;
					
					JSONObject policy = getPolicy(mailbox_id, "new_customer_policy");
					if(policy == null)
						continue;
					
					Principal p = PrincipalService.getPrincipal((Long)entry.get("operator_id"));
					if(VysperServer.instance().isUserOnline(p.getLoginId() +"@"+ p.getDomainName()) && !is_seen){
						//online_unread_timeout ����״̬�£��¿ͻ��ʼ���� xx ���Ӻ�δ�鿴���Զ�ת������������Ա
						long online_unread_timeout = policy.optLong("online_unread_timeout");
						Date now = new Date();
						long diff = getTimeDiff(now, recv_time);
						if(diff >= online_unread_timeout){
							JSONObject nextUser = getNextUser(p.getId(), distributor);
							if(nextUser == null)
								continue;
							
							if(nextUser.optLong("id") == p.getId())
								continue;
							
							resultHandle(p, entry, nextUser, "����״̬�£��¿ͻ��ʼ����  "+online_unread_timeout+" ���Ӻ�δ�鿴���Զ�ת������������Ա");
						}
					}else if(!is_seen){
						//offline_timeout ����״̬�£�δ�鿴���¿ͻ��ʼ�����ת�Ƹ�������xx���ӵ���Ա���д���
						long offline_timeout = policy.optLong("offline_timeout");
						
						for (int i = 0; i < distributor.length(); i++) {
							JSONObject item = distributor.optJSONObject(i);
							if(item == null)
								continue;
							long onlineTime = VysperServer.instance().getUserOnlineTime(item.optString("loginId"));
							if(onlineTime >= (offline_timeout * 60)){
								resultHandle(p, entry, item, "����״̬��, δ�鿴���¿ͻ��ʼ�����ת�Ƹ�������"+offline_timeout+"���ӵ���Ա���д���");
								break;
							}
						}
						
					}else if(!is_handled){
						//unhandled_timeout �Ѳ鿴�ʼ�δ��xx����֮�ڽ��лظ�������Զ�ת�Ƹ�������Ա����
						Date seen_time = (Date)entry.get("seen_time");
						if(seen_time == null)
							continue;
						long unhandled_timeout = policy.optLong("unhandled_timeout");
						Date now = new Date();
						long diff = getTimeDiff(now, seen_time);
						if(diff >= unhandled_timeout){
							JSONObject nextUser = getNextUser(p.getId(), distributor);
							if(nextUser == null)
								continue;
							
							if(nextUser.optLong("id") == p.getId())
								continue;
							
							resultHandle(p, entry, nextUser, "�Ѳ鿴�ʼ�δ��  "+unhandled_timeout+" ����֮�ڽ��лظ�������Զ�ת�Ƹ�������Ա����");
						}
					}
				} catch (Exception e) {
					e.printStackTrace();
				}
			}
		}
		
		/**
		 * �����Ͽͻ���¼
		 */
		private void oldContactMailHandle(){
			//�Ͽͻ��ʼ�����
			Iterator<Map<String,Object>>it = oldBoxViews.iterator();
			while (it.hasNext()) {
				Map<String, Object> entry = it.next();
				
				try {
					Boolean is_seen = entry.get("is_seen") == null ? false : (Boolean)entry.get("is_seen");
					Date recv_time = (Date)entry.get("recv_time");
					//��ȡ�������
					Long mailbox_id = (Long)entry.get("mail_box_id");
					JSONArray distributor = getDistributor(mailbox_id);
					if(distributor == null)
						continue;
					
					JSONObject policy = getPolicy(mailbox_id, "old_customer_policy");
					if(policy == null)
						continue;
					Principal p = PrincipalService.getPrincipal((Long)entry.get("operator_id"));
					if(!is_seen){
						//unread_timeout ���ʼ����xxСʱ��δ�鿴�����Զ����ʼ�ת�Ƹ�������Ա����
						long unread_timeout = policy.optLong("unread_timeout");
						Date now = new Date();
						long diff = getTimeDiff(now, recv_time) / 60;
						if(diff >= unread_timeout){
							JSONObject nextUser = getNextUser(p.getId(), distributor);
							if(nextUser == null)
								continue;
							
							if(nextUser.optLong("id") == p.getId())
								continue;
							
							resultHandle(p, entry, nextUser, "���ʼ����"+unread_timeout+"Сʱ��δ�鿴�����Զ����ʼ�ת�Ƹ�������Ա����");
						}
					}else {
						//unhandled_timeout δ��xxСʱ֮�ڽ��лظ�������Ѳ鿴�ʼ������Զ�ת�Ƹ�������Ա����
						Date seen_time = (Date)entry.get("seen_time");
						if(seen_time == null)
							continue;
						long unhandled_timeout = policy.optLong("unhandled_timeout");
						Date now = new Date();
						long diff = getTimeDiff(now, seen_time) / 60;
						if(diff >= unhandled_timeout){
							JSONObject nextUser = getNextUser(p.getId(), distributor);
							if(nextUser == null)
								continue;
							
							if(nextUser.optLong("id") == p.getId())
								continue;
							
							resultHandle(p, entry, nextUser, "δ��"+unhandled_timeout+"Сʱ֮�ڽ��лظ�������Ѳ鿴�ʼ������Զ�ת�Ƹ�������Ա����");
						}
					}
				} catch (Exception e) {
					e.printStackTrace();
				}
			}
		}
		
		/**
		 * �Է�������з��������ļ�¼��һϵ���޸�
		 * @param map
		 * @param nextUser
		 * @param move_info
		 */
		private void resultHandle(Principal p, Map<String, Object> map, JSONObject nextUser, String move_info) {
			SessionContext ctx = null;
			try {
				ctx = EntityManagerUtil.currentContext();
				Session hsession = ctx.getHibernateSession();
				StringBuilder sb = new StringBuilder();
				Transaction tx = hsession.getTransaction();
				boolean owner_tx = !tx.isActive();
				try {
					if (owner_tx)
						tx.begin();
					Long id = (Long) map.get("id");
					map.remove("id");
					Map<String, Object> entityMap = new HashMap<String, Object>();
					Iterator<Map.Entry<String, Object>> itEntry = map.entrySet().iterator();
					while (itEntry.hasNext()) {
						Map.Entry<String, Object> _entry = itEntry.next();
						entityMap.put(_entry.getKey(), _entry.getValue());
					}
					entityMap.remove("$type$");
					entityMap.put("operator_id", nextUser.optLong("id"));
					entityMap.put("operator_name", nextUser.optString("name"));
					entityMap.put("is_seen", Boolean.FALSE);
					entityMap.put("is_handled", Boolean.FALSE);
					entityMap.put("is_owner", Boolean.TRUE);
					entityMap.put("recv_time", new Date());

					if(!LoginSession.isLogined()){
						@SuppressWarnings("unused")
						LoginSession loginSession = new LoginSession();
						LoginSession.backUse(null, ime.core.Reserved.BACKUSER_ID, ime.core.Reserved.MAIN_DOMAIN_ID);
					}
					hsession.merge("ML_MailBoxView", entityMap);
					
					//��̬�����ʼ�֪ͨ
					Principal to = PrincipalService.getPrincipal(nextUser.optLong("id"));
					sb.setLength(0);
					sb.append("<message type=\"MailDsBoxDeliver\" >")
					  .append("<subject>").append("�����·�����ʼ�").append("</subject>")
					  .append("<body>")
						  .append("<message>").append("�����·�����ʼ�").append("</message>")
					  .append("</body>")
					  .append("<icon></icon>")
					  .append("</message>");
					MailManager.getInstance().AddMessage(to.getLoginId() + "@" + to.getDomainName(), sb.toString());

					sb.setLength(0);
					sb.append("update ML_MailBoxView set move_to_id=:move_to_id, move_to_name=:move_to_name, is_owner=:is_owner,move_info=:move_info, move_time=:move_time where id=:id");
					Query query = hsession.createQuery(sb.toString());
					query.setParameter("move_to_id", nextUser.optLong("id"));
					query.setParameter("move_to_name", nextUser.optString("name"));
					query.setParameter("is_owner", Boolean.FALSE);
					query.setParameter("move_info", move_info);
					query.setParameter("move_time", new Date());
					query.setParameter("id", id);
					query.executeUpdate();
					
					sb.setLength(0);
					sb.append("update ML_MailEntry set operator_id=:operator_id, operator_name=:operator_name where id=:id");
					query = hsession.createQuery(sb.toString());
					query.setParameter("operator_id", nextUser.optLong("id"));
					query.setParameter("operator_name", nextUser.optString("name"));
					query.setParameter("id", map.get("mail_entry_id"));
					query.executeUpdate();
					
					sb.setLength(0);
					sb.append("<message type=\"MailMoveDeliver\" >")
					  .append("<subject>").append("���б��ƶ����ʼ�").append("</subject>")
					  .append("<body>")
						  .append("<message>").append("�����ʼ�").append(entityMap.get("mail_entry_label")).append("��ת�Ƹ�").append(nextUser.optString("name")).append("����").append("</message>")
					  .append("</body>")
					  .append("<icon></icon>")
					  .append("</message>");
					MailManager.getInstance().AddMoveMessage(p.getLoginId() + "@" + p.getDomainName(), sb.toString());
					
					if (owner_tx)
						tx.commit();
				} catch (Exception e) {
					if (owner_tx)
						tx.rollback();
					throw e;
				}
			} catch (Exception e) {
				e.printStackTrace();
			} finally {
				EntityManagerUtil.closeSession(ctx);
			}
		}
		
		/**
		 * ��ȡ���Ͽͻ���Ӧ������
		 * @param mailbox_id
		 * @param type
		 * @return
		 */
		private JSONObject getPolicy(Long mailbox_id, String type){
			JSONObject config_data = policyMap.get(mailbox_id);
			if(config_data != null && config_data.has("distribution_policy")){
				JSONObject distribution_policy = config_data.optJSONObject("distribution_policy");
				if(distribution_policy == null || !distribution_policy.has(type)){
					return null;
				}
				
				return distribution_policy.optJSONObject(type);
			}
			
			return null;
		}
		
		/**
		 * ��ȡ������Ա
		 * @param mailbox_id
		 * @return
		 */
		private JSONArray getDistributor(Long mailbox_id){
			JSONObject config_data = policyMap.get(mailbox_id);
			if(config_data != null && config_data.has("distribution_policy")){
				JSONObject distribution_policy = config_data.optJSONObject("distribution_policy");
				if(distribution_policy == null || !distribution_policy.has("distributor"))
					return null;
				
				return distribution_policy.optJSONArray("distributor");
			}
			return null;
		}
		
		/**
		 * ��ȡ����ʱ���ʱ���
		 * @param date1
		 * @param date2
		 * @return
		 */
		private long getTimeDiff(Date date1, Date date2){
			return (date1.getTime() - date2.getTime()) / (1000 * 60);
		}
	}
}
