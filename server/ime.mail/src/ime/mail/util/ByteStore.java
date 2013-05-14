package ime.mail.util;

import java.io.*;
import org.apache.log4j.Logger;

public class ByteStore implements Serializable {
	static final long serialVersionUID = 4689743335269719513L;
	private static Logger log = Logger.getLogger(ByteStore.class);

	byte[] bytes;

	String content_type = null;
	String content_encoding = null;
	String name;
	String description = "";

	public ByteStore(byte[] b) {
		bytes = b;
	}

	public void setDescription(String s) {
		description = s;
	}

	public String getDescription() {
		return description;
	}

	public void setContentType(String s) {
		content_type = s;
	}

	public String getContentType() {
		if (content_type != null) {
			return content_type;
		} else {
			content_type = getMimeType(name);
			return content_type;
		}
	}

	public String getMimeType(String name) {
		String content_type;
		if (name != null && (name.toLowerCase().endsWith("jpg") || name.toLowerCase().endsWith("jpeg"))) {
			content_type = "IMAGE/JPEG";
		} else if (name != null && name.toLowerCase().endsWith("gif")) {
			content_type = "IMAGE/GIF";
		} else if (name != null && name.toLowerCase().endsWith("png")) {
			content_type = "IMAGE/PNG";
		} else if (name != null && name.toLowerCase().endsWith("txt")) {
			content_type = "TEXT/PLAIN";
		} else if (name != null && (name.toLowerCase().endsWith("htm") || name.toLowerCase().endsWith("html"))) {
			content_type = "TEXT/HTML";
		} else {
			content_type = "APPLICATION/OCTET-STREAM";
		}
		return content_type;
	}

	public void setContentEncoding(String s) {
		content_encoding = s;
	}

	public String getContentEncoding() {
		return content_encoding;
	}

	public byte[] getBytes() {
		return bytes;
	}

	public void setName(String s) {
		name = s;
	}

	public String getName() {
		return name;
	}

	public int getSize() {
		return bytes.length;
	}

	/**
	 * Create a ByteStore out of an InputStream
	 */
	public static ByteStore getBinaryFromIS(InputStream in, int nr_bytes_to_read) {
		byte[] s = new byte[nr_bytes_to_read + 100];
		int count = 0;
		int lastread = 0;
		// log.debug("Reading ... ");
		if (in != null) {
			synchronized (in) {
				while (count < s.length) {
					try {
						lastread = in.read(s, count, nr_bytes_to_read - count);
					} catch (EOFException ex) {
						log.error(ex);
						lastread = 0;
					} catch (Exception z) {
						log.error(z.getMessage());
						lastread = 0;
					}
					count += lastread;
					// log.debug(lastread+" ");
					if (lastread < 1)
						break;
				}
			}
			byte[] s2 = new byte[count + 1];
			for (int i = 0; i < count + 1; i++) {
				s2[i] = s[i];
			}
			// log.debug("new byte-array, size "+s2.length);
			ByteStore d = new ByteStore(s2);
			return d;
		} else {
			return null;
		}
	}
}
