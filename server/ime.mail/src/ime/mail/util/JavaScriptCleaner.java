package ime.mail.util;

import org.w3c.dom.*;
import org.w3c.dom.html.*;

/**
 * JavaScriptCleaner.java
 * 
 * This class removes hopefully all of the possible malicious code from HTML
 * messages like <SCRIPT> tags, javascript: hrefs and onMouseOver, ...;
 * 
 * Furthermore, we should consider removing all IMG tags as they might be used
 * to call CGIs
 * 
 * Created: Mon Jan 1 15:20:54 2001
 * 
 * @author Sebastian Schaffert
 */
public class JavaScriptCleaner {
	Document d;

	public JavaScriptCleaner(Document d) {
		this.d = d;
		// walkTree(d.getDocumentElement());
		walkTree(d);
	}

	protected void walkTree(Node node) {
		/*
		 * First we check for element types that shouldn't be sent to the user.
		 * For that, we add an attribute "malicious" that can be handled by the
		 * XSLT stylesheets that display the message.
		 */
		if (node instanceof HTMLScriptElement) {
			((Element) node).setAttribute("malicious", "Marked malicious because of potential JavaScript abuse");
		}

		if (node instanceof HTMLImageElement) {
			((Element) node).setAttribute("malicious", "Marked malicious because of potential Image/CGI abuse");
		}

		/* What we also really don't like in HTML messages are FORMs! */

		if (node instanceof HTMLFormElement) {
			((Element) node).setAttribute("malicious", "Marked malicious because of potential Form abuse");
		}


		if (node.hasChildNodes()) {
			NodeList nl = node.getChildNodes();
			for (int i = 0; i < nl.getLength(); i++) {
				walkTree(nl.item(i));
			}
		}
	}
}
