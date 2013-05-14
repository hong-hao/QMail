package ime.mail;

import java.util.HashMap;
import java.util.Map;

import javax.mail.Message;

/**
 * À¬»øÓÊ¼ş´¦ÀíÆ÷
 * @author honghao
 *
 */
public class SpamManager {
	private static Map<String, String> spamHeader = null;
	
	/**
	 * ÅĞ¶ÏÊÇ·ñÊÇÀ¬»øÓÊ¼ş
	 * @param message
	 * @return true/false
	 * @throws Exception
	 */
	public static boolean isSpamMail(Message message) throws Exception{
		if( spamHeader == null )
			loadSpamHeader();
		String[] value;
		for(String header : spamHeader.keySet()){
			value = message.getHeader(header);
			if( value != null && value.length > 0 && value[0] != null ){
				if( spamHeader.get(header).equalsIgnoreCase(value[0].trim()) )
					return true;
			}
		}
		return false;
	}
	private static void loadSpamHeader(){
		spamHeader = new HashMap<String, String>();
		String header = Extension.getProperty("SPAM.Header");
		if( header != null ){
			String[] headers = header.split(";");
			for(String h : headers ){
				String[] part = h.split(":");
				if( part.length == 2 ){
					spamHeader.put(part[0].trim(), part[1].trim());
				}
			}
		}
	}
}
