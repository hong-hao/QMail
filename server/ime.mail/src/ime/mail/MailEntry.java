package ime.mail;

import ime.document.DocumentStorage;
import ime.mail.mailbox.IMailBox;
import ime.mail.mailbox.IMailFolder;
import ime.mail.util.Helper;
import ime.mail.xml.XMLMessage;
import ime.mail.xml.XMLMessagePart;

import java.io.BufferedReader;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.FileWriter;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.Reader;
import java.io.StringWriter;
import java.io.UnsupportedEncodingException;
import java.io.Writer;
import java.sql.Timestamp;
import java.util.Enumeration;
import java.util.HashMap;
import java.util.Map;
import java.util.Properties;
import java.util.StringTokenizer;
import java.util.UUID;

import javax.mail.Address;
import javax.mail.Header;
import javax.mail.Message;
import javax.mail.MessagingException;
import javax.mail.Part;
import javax.mail.Session;
import javax.mail.Message.RecipientType;
import javax.mail.internet.MimeMessage;
import javax.mail.internet.MimeMultipart;
import javax.mail.internet.MimeUtility;
import javax.mail.internet.ParseException;
import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;

import org.apache.log4j.Logger;
import org.hibernate.Transaction;
import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.NamedNodeMap;
import org.w3c.dom.Node;

import apf.crypto.Base64;
import apf.util.EntityManagerUtil;
import apf.util.SessionContext;

public class MailEntry {
	private static Logger logger = Logger.getLogger(MailEntry.class);

	public static final String ENTITY_NAME = "ML_MailEntry";
	public static final String ROOT_DIR = "/mail";

	public static final long SEND_MAIL = 1L;
	public static final long RECV_MAIL = 2L;
	public static final long DRAFT_MAIL = 3L;
	public static final long DELETED_MAIL = 4L;
	public static final long JUNK_MAIL = 5L;
	public static final long CLEANED_MAIL = 6L;
	public static final long OUTBOXMAIL = 6L;

	public enum Flag {
		ANSWERED, DELETED, DRAFT, RECENT, SEEN
	}

	private Map<String, Object> entity;
	private MimeMessage mailMessage;
	private IMailBox mailBox;
	private boolean mailMessageChanged = false;
	private String defaultCharset = "GBK";
	
	public MailEntry(IMailBox mailBox) {
		this.mailBox = mailBox;
		this.entity = new HashMap<String, Object>();
	}

	public MailEntry(IMailBox mailBox, Map<String, Object> entity) {
		this.mailBox = mailBox;
		this.entity = entity;
	}

	public MimeMessage getMailMessage() {
		if (this.mailMessage == null) {
			try {
				this.read();
			} catch (Exception e) {
			}
		}
		return this.mailMessage;
	}

	public Map<String, Object> getEntity() {
		return this.entity;
	}

	public void setMailFolder(IMailFolder folder) {
		if (folder != null) {
			this.entity.put("folder_id", folder.getId());
			this.entity.put("folder_label", folder.getName());
		} else {
			this.entity.put("folder_id", null);
			this.entity.put("folder_label", null);
		}
	}

	public void addFlag(Flag flag) {
		addFlag(flag.toString());
	}

	public void addFlag(String flag) {
		String flags = (String) entity.get("flags");
		if (flags == null)
			flags = flag;
		else
			flags += "," + flag;
		entity.put("flags", flags);
	}
	public static String decodeText(String text){
		try {
			if( text.indexOf("=?") != -1 )
				text = MimeUtility.decodeText(text);
			if( text.indexOf("=?") != -1 )
				text = MimeUtility.decodeWord(text);
		} catch (UnsupportedEncodingException e) {
		} catch (ParseException e) {
		}
		return text;
	}
	public void removeFlag(Flag flag) {
		removeFlag(flag.toString());
	}

	public void removeFlag(String flag) {
		String flags = (String) entity.get("flags");
		if (flags == null)
			return;
		String[] parts = flags.split(",");
		StringBuilder sb = new StringBuilder();
		for (String i : parts) {
			if (i.equals(flag))
				continue;
			if (sb.length() > 0)
				sb.append(",");
			sb.append(i);
		}
		entity.put("flags", sb.toString());
	}

	public boolean hasFlag(Flag flag) {
		return hasFlag(flag.toString());
	}

	public boolean hasFlag(String flag) {
		String flags = (String) entity.get("flags");
		if (flags == null)
			return false;
		String[] parts = flags.split(",");
		for (String i : parts) {
			if (i.equals(flag))
				return true;
		}
		return false;
	}

	public void setMailType(long type) {
		entity.put("mail_type", type);
	}

	public long getMailType() {
		Long type = (Long) entity.get("mail_type");
		if (type == null)
			return 0;
		return type.longValue();
	}

	private void putEntityField(String field, String value, int maxLen) {
		if (value != null && value.length() > maxLen)
			entity.put(field, value.substring(maxLen));
		else
			entity.put(field, value);
	}

	private void putEntityField(String field, Address value, int maxLen) {
		if (value == null) {
			entity.put(field, null);
			return;
		}
		String strValue = value.toString();
		try {
			strValue = new String(strValue.getBytes("ISO8859_1"), defaultCharset);
		} catch (UnsupportedEncodingException e) {
		}
		strValue = decodeText(strValue);
		if (strValue.length() > maxLen)
			entity.put(field, strValue.substring(maxLen));
		else
			entity.put(field, strValue);
	}

	private void putEntityField(String field, Address[] addresses, int maxLen) {
		if (addresses == null) {
			entity.put(field, null);
			return;
		}
		String strValue;
		StringBuilder sb = new StringBuilder();
		for (Address address : addresses) {
			strValue = address.toString();
			try {
				strValue = new String(strValue.getBytes("ISO8859_1"), defaultCharset);
			} catch (UnsupportedEncodingException e) {
			} 
			strValue = decodeText(strValue);
			sb.append(strValue).append(";");
		}
		strValue = sb.toString();
		if (strValue.length() > maxLen)
			entity.put(field, strValue.substring(maxLen));
		else
			entity.put(field, strValue);
	}
	private String getHeader(String header){
		if( this.mailMessage == null )
			return null;
		String rawvalue = null;
		try {
			rawvalue = this.mailMessage.getHeader(header, null);
			if( rawvalue == null )
				return null;
			if( rawvalue.indexOf("=?") == -1 )
				rawvalue = new String(rawvalue.getBytes("ISO8859_1"), defaultCharset); 
			return decodeText(rawvalue);
		} catch (Exception e) {
		}
		return rawvalue;
	}
	private String decodeHeader(String rawvalue){
		try {
			if( rawvalue == null )
				return null;
			if( rawvalue.indexOf("=?") == -1 )
					rawvalue = new String(rawvalue.getBytes("ISO8859_1"), defaultCharset);
			return decodeText(rawvalue);
		} catch (UnsupportedEncodingException e) {
		}
		return rawvalue;
	}
	public void setMailMessage(MimeMessage message, String uid) throws Exception {
		this.mailMessage = message;
		if (message == null)
			return;
		defaultCharset = getCharset(message.getContentType());
		
		putEntityField("subject", getHeader("Subject"), 500);
		putEntityField("mail_from", message.getFrom(), 300);
		putEntityField("sender", message.getSender(), 100);
		putEntityField("reply_to", message.getReplyTo(), 300);
		putEntityField("mail_to", message.getRecipients(RecipientType.TO), 500);
		putEntityField("cc", message.getRecipients(RecipientType.CC), 300);
		putEntityField("bcc", message.getRecipients(RecipientType.BCC), 100);

		putEntityField("mail_uid", uid, 100);
		entity.put("create_time", new Timestamp(System.currentTimeMillis()));
		
		String uuid = UUID.randomUUID().toString();
		putEntityField("uuid", uuid, 100);
		
		String mailFile;
		StringBuilder sb = new StringBuilder();
		String path = DocumentStorage.getFilePath(uuid);
		sb.append(ROOT_DIR).append(path).append("/");
		File dir = new File(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, sb.toString()));
		dir.mkdirs();
		sb.append(uuid);
		mailFile = sb.toString() + ".eml";

		entity.put("mail_file", mailFile);
		parseContent();
		
		this.mailMessageChanged = true;
	}
	public void parseContent() throws Exception{
		String path = (String)entity.get("mail_file");
		path = path.replace(".eml", ".parts/");
		
		DocumentBuilderFactory dbfac = DocumentBuilderFactory.newInstance();
        DocumentBuilder docBuilder = dbfac.newDocumentBuilder();
        Document doc = docBuilder.newDocument();
        Element root = doc.createElement("message");
        doc.appendChild(root);
		XMLMessage xmlMessage = new XMLMessage(root);
		
		Enumeration headers = this.mailMessage.getAllHeaders();
		String name, value;
		while (headers.hasMoreElements()) {
			Header h = (Header) headers.nextElement();
			name = h.getName();
			value = decodeHeader(h.getValue());
			xmlMessage.setHeader(name, value);
		}
		
		parseMIMEContent(this.mailMessage, xmlMessage, path);
		
		entity.put("contents", xmlMessage.toXmlString());
	}
	
	@SuppressWarnings("unused")
	private String getContentString(InputStream is, String charset) throws Exception{
		Writer writer = new StringWriter();
		
        char[] buffer = new char[1024];
        try {
            Reader reader = new BufferedReader(
                    new InputStreamReader(is, charset));
            int n;
            while ((n = reader.read(buffer)) != -1) {
                writer.write(buffer, 0, n);
            }
        } finally {
            is.close();
        }
        return writer.toString();
	}
	private String getCharset(String contentType){
		String charset = "GBK";
		StringTokenizer tok2 = new StringTokenizer(contentType, ";=");
		String blah = tok2.nextToken();
		if (tok2.hasMoreTokens()) {
			blah = tok2.nextToken().trim();
			if (blah.toLowerCase().equals("charset") && tok2.hasMoreTokens()) {
				charset = tok2.nextToken().trim();
				if( charset.length() > 0 && charset.charAt(0) == '\"' )
					charset = charset.substring(1);
				if( charset.length() > 0 && charset.charAt(charset.length() - 1) == '\"' )
					charset = charset.substring(0, charset.length() - 1);
			}
		}
		return charset;
	}
	private void saveContentToFile(String folder, String file, InputStream is, String charset) throws Exception{
		File folderFile = new File(folder);
		folderFile.mkdirs();
		
		Writer writer = new FileWriter(folder + "/" + file);
		
        char[] buffer = new char[1024];
        try {
            Reader reader = new BufferedReader(
                    new InputStreamReader(is, charset));
            int n;
            while ((n = reader.read(buffer)) != -1) {
                writer.write(buffer, 0, n);
            }
        } finally {
            is.close();
            writer.close();
        }
	}
	/**
	 * Use depth-first search to go through MIME-Parts recursively.
	 * 
	 * @param p Part to begin with
	 */
	@SuppressWarnings("unused")
	private void parseMIMEContent(Part p, XMLMessagePart parent_part, String mailFolder) throws MessagingException {
		StringBuilder content = new StringBuilder(1000);
		XMLMessagePart xml_part;
		try {
			if (p.getContentType().toUpperCase().startsWith("TEXT/HTML")) {
				/*
				 * The part is a text in HTML format. We will try to use "Neko"
				 * to create a well-formatted XHTML DOM from it and then remove
				 * JavaScript and other "evil" stuff. For replying to such a
				 * message, it will be useful to just remove all of the tags and
				 * display only the text.
				 */
				xml_part = parent_part.createPart("html");
				//xml_part.addContent((String)p.getContent(), 0);
				//xml_part.addContent(getContentString(p.getInputStream(), getCharset(p.getContentType())), 0);
				String file = UUID.randomUUID().toString() + ".html";
				saveContentToFile(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, mailFolder), file, p.getInputStream(), getCharset(p.getContentType()));
				xml_part.addFileContent(file, 0);
			} else if (p.getContentType().toUpperCase().startsWith("TEXT") || p.getContentType().toUpperCase().startsWith("MESSAGE")) {
				/*
				 * The part is a standard message part in some incarnation of
				 * text (html or plain). We should decode it and take care of
				 * some extra issues like recognize quoted parts, filter
				 * JavaScript parts and replace smileys with smiley-icons if the
				 * user has set wantsFancy()
				 */

				xml_part = parent_part.createPart("text");

				BufferedReader in;
				/*
				if (p instanceof MimeBodyPart) {
					int size = p.getSize();
					MimeBodyPart mpb = (MimeBodyPart) p;
					InputStream is = mpb.getInputStream();

					// Workaround for Java or Javamail Bug
					is = new BufferedInputStream(is);
					ByteStore ba = ByteStore.getBinaryFromIS(is, size);
					in = new BufferedReader(new InputStreamReader(new ByteArrayInputStream(ba.getBytes())));
					// End of workaround
					size = is.available();

				} else {
					in = new BufferedReader(new InputStreamReader(p.getInputStream()));
				}
				*/
				in = new BufferedReader(new InputStreamReader(p.getInputStream(), getCharset(p.getContentType())));
				logger.debug("Content-Type: " + p.getContentType());
				
				String token = "";
				int quote_level = 0, old_quotelevel = 0;
				boolean javascript_mode = false;
				/* Read in the message part line by line */
				while ((token = in.readLine()) != null) {
					/* First decode all language and MIME dependant stuff */
					/*
					String charset = getCharset(p.getContentType());

					try {
						token = new String(token.getBytes(), charset);
					} catch (UnsupportedEncodingException ex1) {
						logger.info("Java Engine does not support charset " + charset + ". Trying to convert from MIME ...");

						try {
							charset = MimeUtility.javaCharset(charset);
							token = new String(token.getBytes(), charset);

						} catch (UnsupportedEncodingException ex) {
							logger.warn("Converted charset (" + charset + ") does not work. Using default charset (ouput may contain errors)");
							token = new String(token.getBytes());
						}
					}
					*/
					/*
					 * Here we figure out which quote level this line has,
					 * simply by counting how many ">" are in front of the line,
					 * ignoring all whitespaces.
					 */
					int current_quotelevel = Helper.getQuoteLevel(token);

					/*
					 * When we are in a different quote level than the last
					 * line, we append all we got so far to the part with the
					 * old quotelevel and begin with a clean String buffer
					 */
					if (current_quotelevel != old_quotelevel) {
						xml_part.addContent(content.toString(), old_quotelevel);
						old_quotelevel = current_quotelevel;
						content.setLength(0);
					}
					/*
					 * if (user.wantsBreakLines()) { Enumeration enumVar =
					 * Helper.breakLine(token, user.getMaxLineLength(),
					 * current_quotelevel);
					 * 
					 * while (enumVar.hasMoreElements()) { String s = (String)
					 * enumVar.nextElement(); if (user.wantsShowFancy()) {
					 * content.append(Fancyfier.apply(s)).append("\n"); } else {
					 * content.append(s).append("\n"); } } } else { if
					 * (user.wantsShowFancy()) {
					 * content.append(Fancyfier.apply(token)).append("\n"); }
					 * else { content.append(token).append("\n"); } }
					 */
					content.append(token).append("\n");
				}
				xml_part.addContent(content.toString(), old_quotelevel);
				content.setLength(0);
			} else if (p.getContentType().toUpperCase().startsWith("MULTIPART/ALTERNATIVE")) {
				/*
				 * This is a multipart/alternative part. That means that we
				 * should pick one of the formats and display it for this part.
				 * Our current precedence list is to choose HTML first and then
				 * to choose plain text.
				 */
				MimeMultipart m = (MimeMultipart) p.getContent();
				String[] preferred = { "TEXT/HTML", "TEXT" };
				boolean found = false;
				int alt = 0;
				// Walk though our preferred list of encodings. If we have found
				// a fitting part,
				// decode it and replace it for the parent (this is what we
				// really want with an
				// alternative!)
				/**
				 * findalt: while(!found && alt < preferred.length) { for(int
				 * i=0;i<m.getCount();i++) { Part p2=m.getBodyPart(i);
				 * if(p2.getContentType
				 * ().toUpperCase().startsWith(preferred[alt])) {
				 * parseMIMEContent(p2,parent_part,msgid); found=true; break
				 * findalt; } } alt++; }
				 **/
				/**
				 * When user try to reply a mail, there may be 3 conditions: 1.
				 * only TEXT exists. 2. both HTML and TEXT exist. 3. only HTML
				 * exists.
				 * 
				 * We have to choose which part should we quote, that is, we
				 * must decide the prority of parts to quote. Since quoting HTML
				 * is not easy and precise (consider a html: <body><div><b>some
				 * text..</b> </div></body>. Even we try to get text node under
				 * <body>, we'll just get nothing, because "some text..." is
				 * marked up by <div><b>. There is no easy way to retrieve text
				 * from html unless we parse the html to analyse its semantics.
				 * 
				 * Here is our policy for alternative part: 1. Displays HTML but
				 * hides TEXT. 2. When replying this mail, try to quote TEXT
				 * part. If no TEXT part exists, quote HTML in best effort(use
				 * XMLMessagePart.quoteContent() by Sebastian Schaffert.)
				 */
				while (alt < preferred.length) {
					for (int i = 0; i < m.getCount(); i++) {
						Part p2 = m.getBodyPart(i);
						if (p2.getContentType().toUpperCase().startsWith(preferred[alt])) {
							logger.debug("Processing: " + p2.getContentType());
							parseMIMEContent(p2, parent_part, mailFolder);
							found = true;
							break;
						}
					}
					/**
					 * If we've selected HTML part from alternative part, the
					 * TEXT part should be hidden from display but keeping in
					 * XML for later quoting operation.
					 * 
					 * Of course, this requires some modification on
					 * showmessage.xsl.
					 */
					if (found && (alt == 1)) {
						Node textPartNode = parent_part.getPartElement().getLastChild();
						NamedNodeMap attributes = textPartNode.getAttributes();

						for (int i = 0; i < attributes.getLength(); ++i) {
							Node attr = attributes.item(i);
							// If type=="TEXT", add a hidden attribute.
							if (attr.getNodeName().toUpperCase().equals("TYPE") && attr.getNodeValue().toUpperCase().equals("TEXT")) {
								((Element) textPartNode).setAttribute("hidden", "true");
							}
						}
					}
					alt++;
				}
				if (!found) {
					// If we didn't find one of our preferred encodings, choose
					// the first one
					// simply pass the parent part because replacement is what
					// we really want with
					// an alternative.
					parseMIMEContent(m.getBodyPart(0), parent_part, mailFolder);
				}

			} else if (p.getContentType().toUpperCase().startsWith("MULTIPART/")) {
				/*
				 * This is a standard multipart message. We should recursively
				 * walk thorugh all of the parts and decode them, appending as
				 * children to the current part
				 */

				xml_part = parent_part.createPart("multi");

				MimeMultipart m = (MimeMultipart) p.getContent();
				for (int i = 0; i < m.getCount(); i++) {
					parseMIMEContent(m.getBodyPart(i), xml_part, mailFolder);
				}
			} else {
				/*
				 * Else treat the part as a binary part that the user should
				 * either download or get displayed immediately in case of an
				 * image
				 */
				InputStream in = null;
				String type = "";
				if (p.getContentType().toUpperCase().startsWith("IMAGE/JPG") || p.getContentType().toUpperCase().startsWith("IMAGE/JPEG")) {
					type = "jpg";
					xml_part = parent_part.createPart("image");
				} else if (p.getContentType().toUpperCase().startsWith("IMAGE/GIF")) {
					type = "gif";
					xml_part = parent_part.createPart("image");
				} else if (p.getContentType().toUpperCase().startsWith("IMAGE/PNG")) {
					type = "png";
					xml_part = parent_part.createPart("image");
				} else {
					xml_part = parent_part.createPart("binary");
				}
				int size = p.getSize();
				/*
				if (p instanceof MimeBodyPart) {
					MimeBodyPart mpb = (MimeBodyPart) p;
					logger.debug("MIME Body part (image), Encoding: " + mpb.getEncoding());
					InputStream is = mpb.getInputStream();

					// Workaround for Java or Javamail Bug
					in = new BufferedInputStream(is);
					ByteStore ba = ByteStore.getBinaryFromIS(in, size);
					in = new ByteArrayInputStream(ba.getBytes());
					// End of workaround
					size = in.available();

				} else {
					logger.warn("No MIME Body part!!");
					// Is this unexpected? Consider changing log level.
					in = p.getInputStream();
				}
				*/
				in = p.getInputStream();
				
				String name = p.getFileName();
				if (name == null || name.equals("")) {
					// Try an other way
					String headers[] = p.getHeader("Content-Disposition");
					int pos = -1;
					if (headers.length == 1) {
						pos = headers[0].indexOf("filename*=") + 10;
					}
					if (pos != -1) {
						int charsetEnds = headers[0].indexOf("''", pos);
						String charset = headers[0].substring(pos, charsetEnds);
						String encodedFileName = headers[0].substring(charsetEnds + 2);
						encodedFileName = "=?" + charset + "?Q?" + encodedFileName.replace('%', '=') + "?=";
						name = MimeUtility.decodeText(encodedFileName);
					} else {
						name = "unknown." + type;
					}
				}
				else
					name = MimeUtility.decodeText(name);
				// Eliminate space characters. Should do some more things in the
				// future
				name = name.replace(' ', '_');
				
				saveFileTo(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, mailFolder), name, in);
				
				/**
				 * For multibytes language system, we have to separate filename
				 * into 2 format: one for display (UTF-8 encoded), another for
				 * encode the url of hyperlink. `filename' is for display, while
				 * `hrefFileName' is for hyperlink. To make use of these two
				 * attributes, `showmessage.xsl' is slightly modified.
				 */
				xml_part.setAttribute("filename", name);
				// Transcode name into UTF-8 bytes then make a new ISO8859_1
				// string to encode URL.
				xml_part.setAttribute("hrefFileName", name);
				xml_part.setAttribute("size", size + "");
				String description = p.getDescription() == null ? "" : p.getDescription();
				xml_part.setAttribute("description", description);
				StringTokenizer tok = new StringTokenizer(p.getContentType(), ";");
				xml_part.setAttribute("content-type", tok.nextToken().toLowerCase());
			}
		} catch (java.io.IOException ex) {
			logger.error("Failed to parse mime content", ex);
		} catch (MessagingException ex) {
			logger.error("Failed to parse mime content", ex);
		} catch (Exception ex) {
			logger.error("Failed to parse mime content", ex);
		}
	}

	private void saveFileTo(String folder, String fileName, InputStream inputStream) throws Exception {
		File folderFile = new File(folder);
		folderFile.mkdirs();

		FileOutputStream output = null;
		try {
			output = new FileOutputStream(folder + fileName);
			byte[] buffer = new byte[1024];
			int len;
			while ((len = inputStream.read(buffer)) > 0) {
				output.write(buffer, 0, len);
			}
		} catch (Exception e) {
			throw e;
		} finally {
			if (output != null)
				output.close();
		}
	}

	@SuppressWarnings("unused")
	private void saveFile(String fileName, InputStream inputStream) throws Exception {
		String rootPath = DocumentStorage.getAbsolutePath("", "attachments");
		String fileExt = null;
		int index = fileName.lastIndexOf(".");
		if (index > 0)
			fileExt = fileName.substring(index + 1);

		String repath = DocumentStorage.getFilePath(fileName);

		StringBuilder sb = new StringBuilder();
		sb.append(rootPath).append(repath);

		String path = sb.toString();
		File dir = new File(path);
		if (!dir.exists())
			dir.mkdirs();

		sb.setLength(0);
		sb.append(Long.toHexString(System.currentTimeMillis())).append(Long.toHexString(Math.round(Math.random() * 100)));

		String fileuid = sb.toString();

		sb.setLength(0);
		sb.append(path).append("/").append(fileuid);

		if (fileExt != null)
			sb.append(".").append(fileExt);

		FileOutputStream output = null;
		try {
			output = new FileOutputStream(sb.toString());
			byte[] buffer = new byte[1024];
			int len;
			while ((len = inputStream.read(buffer)) > 0) {
				output.write(buffer, 0, len);
			}
		} catch (Exception e) {
			throw e;
		} finally {
			if (output != null)
				output.close();
		}

		sb.setLength(0);
		sb.append(repath).append("/").append(fileuid);

		if (fileExt != null)
			sb.append(".").append(fileExt);

		path = sb.toString();

		sb.setLength(0);
		String attachements = (String) entity.get("attachments");
		if (attachements != null)
			sb.append(attachements).append(";");
		sb.append(fileName).append(":").append(path);
		entity.put("attachments", sb.toString());
	}

	public void setMailMessageChanged(boolean changed) {
		this.mailMessageChanged = changed;
	}

	public boolean isMailMessageChanged() {
		return this.mailMessageChanged;
	}

	/**
	 * 读取邮件内容，包括邮件记录和邮件文件
	 * 
	 * @throws Exception
	 */
	@SuppressWarnings("unchecked")
	public void read() throws Exception {
		if (!entity.containsKey("mail_file")) {
			SessionContext ctx = null;
			try {
				ctx = EntityManagerUtil.currentContext();
				org.hibernate.Session hsession = ctx.getHibernateSession();

				StringBuilder sb = new StringBuilder();
				sb.append("from ").append(ENTITY_NAME).append(" where id = :entityId");

				org.hibernate.Query query = hsession.createQuery(sb.toString());
				query.setParameter("entityId", entity.get("id"));

				entity = (Map<String, Object>) query.uniqueResult();
			} catch (Exception e) {
				logger.error(e.getMessage(), e);
				throw e;
			} finally {
				EntityManagerUtil.closeSession(ctx);
			}
		}
		String file = (String) entity.get("mail_file");
		if (file != null) {
			File emlFile = new File(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, file));
			mailMessage = MailEntry.readMail(mailBox.getMailSession(), emlFile);
			mailMessageChanged = false;
		}
	}

	/**
	 * 保存邮件内容，包括邮件记录和邮件文件
	 * 
	 * @throws Exception
	 */
	public void save() throws Exception {
		if (this.mailMessage != null && this.mailMessageChanged) {
			// 获取邮件文件路径
			String file = (String) entity.get("mail_file");
			if (file == null || file.length() == 0) {
				StringBuilder sb = new StringBuilder();
				String uid = UUID.randomUUID().toString();
				String path = DocumentStorage.getFilePath(uid);
				sb.append(ROOT_DIR).append(path).append("/");
				File dir = new File(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, sb.toString()));
				dir.mkdirs();

				sb.append(uid).append(".eml");
				file = sb.toString();
				entity.put("mail_file", file);
			}
			// 保存邮件文件
			File emlFile = new File(DocumentStorage.getAbsolutePath(DocumentStorage.ATTACHEMENTS, file));
			emlFile.getParentFile().mkdirs();

			MailEntry.saveMail(mailMessage, emlFile);

			mailMessageChanged = false;
		}

		// 保存邮件记录
		SessionContext ctx = null;
		try {
			ctx = EntityManagerUtil.currentContext();

			org.hibernate.Session hsession = ctx.getHibernateSession();

			Transaction tx = hsession.getTransaction();
			boolean owner_tx = !tx.isActive();
			try {
				if (owner_tx)
					tx.begin();

				if (entity.containsKey("id"))
					hsession.merge(ENTITY_NAME, entity);
				else
					hsession.persist(ENTITY_NAME, entity);

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

	public String getSubject() {
		if (this.mailMessage != null) {
			String rawvalue = null;
			try {
				rawvalue = this.mailMessage.getHeader("Subject", null);
				if( rawvalue == null )
					return null;
				if( rawvalue.indexOf("=?") == -1 )
					rawvalue = new String(rawvalue.getBytes("ISO8859_1"), defaultCharset); 
				return decodeText(rawvalue);
			} catch (Exception e) {
			}
			return rawvalue;
		}
		return (String) entity.get("subject");
	}

	public String getUid() {
		return (String) entity.get("mail_uid");
	}

	public static MimeMessage readMail(Session mailSession, File emlFile) throws Exception {
		InputStream source = new FileInputStream(emlFile);
		MimeMessage message = new MimeMessage(mailSession, source);
		return message;
	}

	public static void saveMail(Message message, File emlFile) throws Exception {
		OutputStream out = new FileOutputStream(emlFile);
		try {
			message.writeTo(out);
		} finally {
			if (out != null) {
				out.flush();
				out.close();
			}
		}
	}
	
	public static void main(String[] args){
		MailEntry m = new MailEntry(null);
		try {
			Properties props = new Properties();
			props.put("mail.mime.charset", "GBK");
			props.put("mail.mime.decodetext.strict", "false");
			Session session = Session.getDefaultInstance(props, null);
			
			MimeMessage msg = MailEntry.readMail(session, new File("F:/eclipse_workspace/.metadata/.plugins/org.eclipse.wst.server.core/tmp0/wtpwebapps/IMEServer/docroot/attachments/mail/c11/c5/024483DA754B575A63906EC9024A3E93AE00000000000001.eml"));
			
			DocumentBuilderFactory dbfac = DocumentBuilderFactory.newInstance();
	        DocumentBuilder docBuilder = dbfac.newDocumentBuilder();
	        Document doc = docBuilder.newDocument();
	        Element root = doc.createElement("message");
	        doc.appendChild(root);
			XMLMessage xmlMessage = new XMLMessage(root);
			String encoding = msg.getEncoding();
			
			String subject = msg.getSubject();
			String[] h = msg.getHeader("X-ad-flag");
			h = msg.getHeader("X-IronPort-Anti-Spam-Result");
			
			String r = new String(Base64.decode(h[0]));
			subject = new String(subject.getBytes("ISO8859_1"), m.getCharset(msg.getContentType())); 
			long t1 = System.currentTimeMillis();
			m.parseMIMEContent(msg, xmlMessage, "f:/temp");
			long t2 = System.currentTimeMillis();
			System.out.println(t2 - t1);
			//System.out.println(xmlMessage.toXmlString());
		} catch (Exception e) {
			// TODO Auto-generated catch block
			e.printStackTrace();
		}
	}
}
