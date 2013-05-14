package ime.mail.xml;

import java.io.StringWriter;
import java.util.*;

import javax.xml.transform.OutputKeys;
import javax.xml.transform.Transformer;
import javax.xml.transform.TransformerFactory;
import javax.xml.transform.dom.DOMSource;
import javax.xml.transform.stream.StreamResult;

import org.w3c.dom.*;
import org.directwebremoting.util.Logger;

/**
 * A message part object for an XML message
 */
public class XMLMessagePart {
	private static Logger log = Logger.getLogger(XMLMessagePart.class);
	
	protected Document root;
	protected Element part;

	/**
	 * Create a new part for the given root document. Creates the necessary
	 * Element.
	 */
	public XMLMessagePart(Document root) {
		this.part = root.createElement("PART");
		this.root = root;
	}

	/**
	 * Return a new part for a given part element
	 */
	public XMLMessagePart(Element part) {
		this.part = part;
		this.root = part.getOwnerDocument();
	}

	public Element getPartElement() {
		return part;
	}

	public void setAttribute(String key, String value) {
		part.setAttribute(key, value);
	}

	public String getAttribute(String key) {
		return part.getAttribute(key);
	}

	public void quoteContent() {
		NodeList nl = part.getChildNodes();
		StringBuilder text = new StringBuilder();
		for (int i = 0; i < nl.getLength(); i++) {
			Element elem = (Element) nl.item(i);
			if (elem.getNodeName().equals("CONTENT")) {
				String value = XMLCommon.getElementTextValue(elem);
				StringTokenizer tok = new StringTokenizer(value, "\n");
				while (tok.hasMoreTokens()) {
					text.append("> ").append(tok.nextToken()).append("\n");
				}
			}
		}
		removeAllContent();

		addContent(text.toString(), 0);
	}

	/**
	 * This method is designed for content that already is in DOM format, like
	 * HTML messages.
	 */
	public void addContent(Document content) {
		log.fatal("addContenting", new Throwable("CONTENTing"));
		Element content_elem = root.createElement("CONTENT");
		content_elem.setAttribute("quotelevel", "0");

		// The case of "BodY" is funny below to make it certain that the
		// getElementsByTagName() method is case-insensitive (it didn't
		// used to be).
		NodeList nl = content.getDocumentElement().getElementsByTagName("BodY");
		log.debug("While parsing HTML content: Found " + nl.getLength() + " body elements");
		for (int i = 0; i < nl.getLength(); i++) {
			NodeList nl2 = nl.item(i).getChildNodes();
			log.debug("While parsing HTML content: Found " + nl2.getLength() + " child elements");
			for (int j = 0; j < nl2.getLength(); j++) {
				log.debug("Element: " + j);
				content_elem.appendChild(XMLCommon.importNode(root, nl2.item(j), true));
			}
		}

		part.appendChild(content_elem);

		// XMLCommon.debugXML(root);
	}

	public void addContent(String content, int quotelevel) {
		Element content_elem = root.createElement("CONTENT");
		content_elem.setAttribute("quotelevel", quotelevel + "");
		XMLCommon.setElementTextValue(content_elem, content, true);
		part.appendChild(content_elem);
	}
	public void addFileContent(String file, int quotelevel) {
		Element content_elem = root.createElement("CONTENT");
		content_elem.setAttribute("quotelevel", quotelevel + "");
		content_elem.setAttribute("file", file);
		part.appendChild(content_elem);
	}

	public void insertContent(String content, int quotelevel) {
		Element content_elem = root.createElement("CONTENT");
		content_elem.setAttribute("quotelevel", quotelevel + "");
		XMLCommon.setElementTextValue(content_elem, content, true);
		Node first = part.getFirstChild();
		part.insertBefore(content_elem, first);
	}

	public void addJavaScript(String content) {
		Element javascript_elem = root.createElement("JAVASCRIPT");
		XMLCommon.setElementTextValue(javascript_elem, content, true);
		part.appendChild(javascript_elem);
	}

	public void removeAllContent() {
		XMLCommon.genericRemoveAll(part, "CONTENT");
	}

	public XMLMessagePart createPart(String type) {
		XMLMessagePart newpart = new XMLMessagePart(root);
		newpart.setAttribute("type", type);
		appendPart(newpart);
		return newpart;
	}

	public void insertPart(XMLMessagePart childpart) {
		Node first = part.getFirstChild();
		part.insertBefore(childpart.getPartElement(), first);
	}

	public void appendPart(XMLMessagePart childpart) {
		part.appendChild(childpart.getPartElement());
	}

	public Enumeration<XMLMessagePart> getParts() {
		// Sucking NodeList needs a Vector to store Elements that will be
		// removed!
		Vector<XMLMessagePart> v = new Vector<XMLMessagePart>();
		NodeList parts = part.getChildNodes();
		for (int j = 0; j < parts.getLength(); j++) {
			Element elem = (Element) parts.item(j);
			if (elem.getTagName().equals("PART"))
				v.addElement(new XMLMessagePart(elem));
		}
		return v.elements();
	}

	public void removePart(XMLMessagePart childpart) {
		part.removeChild(childpart.getPartElement());
	}

	public void removeAllParts() {
		XMLCommon.genericRemoveAll(part, "PART");
	}
	public String toXmlString(){
		try {
	        TransformerFactory transfac = TransformerFactory.newInstance();
	        Transformer trans;
			trans = transfac.newTransformer();
	        trans.setOutputProperty(OutputKeys.OMIT_XML_DECLARATION, "yes");
	        trans.setOutputProperty(OutputKeys.INDENT, "yes");

	        StringWriter sw = new StringWriter();
	        StreamResult result = new StreamResult(sw);
	        DOMSource source = new DOMSource(root);
	        trans.transform(source, result);
	        return sw.toString();
		} catch (Exception e) {
			return "";
		}
	}
}
