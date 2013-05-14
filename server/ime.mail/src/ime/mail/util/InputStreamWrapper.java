package ime.mail.util;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.UnsupportedEncodingException;

public class InputStreamWrapper extends InputStream {
	private BufferedReader reader;
	public InputStreamWrapper(InputStream is, String charset) throws UnsupportedEncodingException{
		reader = new BufferedReader(new InputStreamReader(is, charset));
	}
	@Override
	public int read() throws IOException {
		return reader.read();
	}

}
