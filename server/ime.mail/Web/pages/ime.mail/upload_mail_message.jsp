<%@ page language="java" contentType="text/html; charset=utf-8" pageEncoding="utf-8" import="ime.security.*,ime.document.*,java.util.*,java.io.*,com.oreilly.servlet.multipart.*"%>
<%
	String ATTACHEMENT_PATH = "attachments";
	try {
		String uuid = request.getParameter("uuid");
		if (uuid == null)
			throw new Exception();
		MultipartParser mp = new MultipartParser(request,10 * 1024 * 1024, false, false, "utf-8");
		
		//TODO 根据邮件UUID上传邮件消息文件，需验证用户权限
		LoginSession.setHttpSession(session);
		if (!LoginSession.isLogined())
			throw new Exception("您尚未登录或超时");
		
		String rootPath = DocumentStorage.getAbsolutePath("",ATTACHEMENT_PATH);
		File rootDir = new File(rootPath);
		if (!rootDir.exists())
			rootDir.mkdirs();
	
		Part part = null;
		StringBuilder sb = new StringBuilder();
	
		while ((part = mp.readNextPart()) != null) {
			if (part.isFile()) {
				FilePart filePart = (FilePart) part;
				String fileName = filePart.getFileName();
	
				if (fileName != null) {
					sb.setLength(0);
					sb.append(rootPath).append(uuid);
					File dir = new File(sb.toString());
					if (!dir.exists())
						dir.mkdirs();
					sb.append("/").append(fileName);
					FileOutputStream output = null;
					try {
						output = new FileOutputStream(sb.toString());
						filePart.writeTo(output);
						output.close();
					} catch (Exception ex) {
						ex.printStackTrace();
					} finally {
						if (output != null)
							out.close();
					}
				}
			}
		}
	} catch (Exception e) {
	}
%>