package ime.mail;

import java.util.HashMap;
import java.util.Map;

import ime.core.ext.IExtension;

public class Extension implements IExtension {
	private static Map<String, String> properties = new HashMap<String, String>();
	
	@Override
	public Map<String, String> getProperties() {
		return properties;
	}
	@Override
	public void setProperties(Map<String, String> prop) {
		properties = prop;
	}

	@Override
	public void handleLoad() throws Exception {
		MailManager.loadMailAccounts();
		MailDistributionPolicy.getInstance().reloadPolicy();
		MailDistributionPolicy.getInstance().dynamicPolicyStart();
	}

	@Override
	public void handleUnload() throws Exception {
	}

	public static String getProperty(String propName){
		if( properties != null )
			return properties.get(propName);
		return null;
	}
}
