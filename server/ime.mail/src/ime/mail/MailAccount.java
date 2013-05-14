package ime.mail;

import ime.security.util.DESUtil;

import java.util.Map;

public class MailAccount {
	public static final int POP3 = 1;
	public static final int IMAP = 2;
	
	private static final String PASSWORD_KEY = "asdEid34*44KSse";
	private Map<String, Object> entity;
	
	public MailAccount(Map<String, Object> entity){
		this.entity = entity;
	}
	public Map<String, Object> getEntity(){
		return this.entity;
	}
	public String getSendAddress(){
		String value = (String)entity.get("send_address");
		if( value != null )
			return value;
		else
			return "";
	}
	public int getSendPort(){
		Long value = (Long)entity.get("send_port");
		if( value != null )
			return value.intValue();
		else
			return 25;
	}
	public boolean isSendSSL(){
		Boolean value = (Boolean)entity.get("is_send_ssl");
		if( value != null )
			return value;
		else
			return false;
	}
	public String getRecvAddress(){
		String value = (String)entity.get("recv_address");
		if( value != null )
			return value;
		else
			return "";
	}
	public int getRecvPort(){
		Long value = (Long)entity.get("recv_port");
		if( value != null )
			return value.intValue();
		else if( isRecvSSL() )
			return 995;
		else
			return 110;
	}
	public boolean isRecvSSL(){
		Boolean value = (Boolean)entity.get("is_recv_ssl");
		if( value != null )
			return value;
		else
			return false;
	}
	public String getAccount(){
		String value = (String)entity.get("account");
		if( value != null )
			return value;
		else
			return "";
	}
	public String getName(){
		String value = (String)entity.get("name");
		if( value != null )
			return value;
		else
			return "";
	}
	public int getRecvType(){
		Long value = (Long)entity.get("recv_type");
		if( value != null )
			return value.intValue();
		else
			return 1;
	}
	public String getPassword() throws Exception{
		String value = (String)entity.get("password");
		if( value == null )
			return "";
		DESUtil desUtil = new DESUtil(PASSWORD_KEY);
		return desUtil.decrypt(value);
	}
	public void setPassword(String rowpass) throws Exception{
		entity.put("password", encodePassword(rowpass));
	}
	public static String encodePassword(String rowpass) throws Exception{
		DESUtil desUtil = new DESUtil(PASSWORD_KEY);
		return desUtil.encrypt(rowpass);
	}
}
