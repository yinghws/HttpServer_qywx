using System;
using System.Collections;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Web;
using System.Xml;
using System.Data.SqlClient;
using System.Linq;
using System.Data;
using System.Timers;
using System.Collections.Generic;


namespace HttpServer_qywx
{

    public class HttpProcessor
    {
        public TcpClient socket;
        public HttpServer srv;

        private Stream inputStream;
        public StreamWriter outputStream;

        public String http_method;
        public String http_url;
        public String http_protocol_versionstring;
        public Hashtable httpHeaders = new Hashtable();


        private static int MAX_POST_SIZE = 10 * 1024 * 1024; // 10MB

        public HttpProcessor(TcpClient s, HttpServer srv)
        {
            this.socket = s;
            this.srv = srv;
        }


        private string streamReadLine(Stream inputStream)
        {
            int next_char;
            string data = "";
            while (true)
            {
                next_char = inputStream.ReadByte();
                if (next_char == '\n') { break; }
                if (next_char == '\r') { continue; }
                if (next_char == -1) { Thread.Sleep(1); continue; };
                data += Convert.ToChar(next_char);
            }
            return data;
        }
        public void process()
        {
            // we can't use a StreamReader for input, because it buffers up extra data on us inside it's
            // "processed" view of the world, and we want the data raw after the headers
            inputStream = new BufferedStream(socket.GetStream());

            // we probably shouldn't be using a streamwriter for all output from handlers either
            outputStream = new StreamWriter(new BufferedStream(socket.GetStream()));
            try
            {
                parseRequest();
                readHeaders();
                if (http_method.Equals("GET"))
                {
                    handleGETRequest();
                }
                else if (http_method.Equals("POST"))
                {
                    handlePOSTRequest();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Exception: " + e.ToString());
                writeFailure();
            }
            outputStream.Flush();
            // bs.Flush(); // flush any remaining output
            inputStream = null; outputStream = null; // bs = null;            
            socket.Close();
        }

        public void parseRequest()
        {
            String request = streamReadLine(inputStream);
            string[] tokens = request.Split(' ');
            if (tokens.Length != 3)
            {
                throw new Exception("invalid http request line");
            }
            http_method = tokens[0].ToUpper();
            http_url = tokens[1];
            http_protocol_versionstring = tokens[2];

            Console.WriteLine("starting: " + request);
        }

        public void readHeaders()
        {
            Console.WriteLine("readHeaders()");
            String line;
            while ((line = streamReadLine(inputStream)) != null)
            {
                if (line.Equals(""))
                {
                    Console.WriteLine("got headers");
                    return;
                }

                int separator = line.IndexOf(':');
                if (separator == -1)
                {
                    throw new Exception("invalid http header line: " + line);
                }
                String name = line.Substring(0, separator);
                int pos = separator + 1;
                while ((pos < line.Length) && (line[pos] == ' '))
                {
                    pos++; // strip any spaces
                }

                string value = line.Substring(pos, line.Length - pos);
                Console.WriteLine("header: {0}:{1}", name, value);
                httpHeaders[name] = value;
            }
        }

        public void handleGETRequest()
        {
            srv.handleGETRequest(this);
        }

        private const int BUF_SIZE = 4096;
        public void handlePOSTRequest()
        {
            // this post data processing just reads everything into a memory stream.
            // this is fine for smallish things, but for large stuff we should really
            // hand an input stream to the request processor. However, the input stream 
            // we hand him needs to let him see the "end of the stream" at this content 
            // length, because otherwise he won't know when he's seen it all! 

            Console.WriteLine("get post data start");
            int content_len = 0;
            MemoryStream ms = new MemoryStream();
            if (this.httpHeaders.ContainsKey("Content-Length"))
            {
                content_len = Convert.ToInt32(this.httpHeaders["Content-Length"]);
                if (content_len > MAX_POST_SIZE)
                {
                    throw new Exception(
                        String.Format("POST Content-Length({0}) too big for this simple server",
                          content_len));
                }
                byte[] buf = new byte[BUF_SIZE];
                int to_read = content_len;
                while (to_read > 0)
                {
                    Console.WriteLine("starting Read, to_read={0}", to_read);

                    int numread = this.inputStream.Read(buf, 0, Math.Min(BUF_SIZE, to_read));
                    Console.WriteLine("read finished, numread={0}", numread);
                    if (numread == 0)
                    {
                        if (to_read == 0)
                        {
                            break;
                        }
                        else
                        {
                            throw new Exception("client disconnected during post");
                        }
                    }
                    to_read -= numread;
                    ms.Write(buf, 0, numread);
                }
                ms.Seek(0, SeekOrigin.Begin);
            }
            Console.WriteLine("get post data end");
            srv.handlePOSTRequest(this, new StreamReader(ms));

        }

        public void writeSuccess()
        {
            outputStream.WriteLine("HTTP/1.0 200 OK");
            outputStream.WriteLine("Content-Type: text/html");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }

        public void writeFailure()
        {
            outputStream.WriteLine("HTTP/1.0 404 File not found");
            outputStream.WriteLine("Connection: close");
            outputStream.WriteLine("");
        }


    }

    public abstract class HttpServer
    {

        protected int port;
        TcpListener listener;
        bool is_active = true;

        public HttpServer(int port)
        {
            this.port = port;
        }

        public void listen()
        {
            listener = new TcpListener(IPAddress.Parse("192.168.8.14"), port);
            listener.Start();
            while (is_active)
            {
                TcpClient s = listener.AcceptTcpClient();
                HttpProcessor processor = new HttpProcessor(s, this);
                Thread thread = new Thread(new ThreadStart(processor.process));
                thread.Start();
                Thread.Sleep(1);
            }
        }

        public abstract void handleGETRequest(HttpProcessor p);
        public abstract void handlePOSTRequest(HttpProcessor p, StreamReader inputData);
    }

    public class Question
    {
        public string user_id;
        public DateTime lasttime;
        public string title;

    }
    public class MyHttpServer : HttpServer
    {
        public MyHttpServer(int port)
            : base(port)
        {
        }




        public List<Question> qt = new List<Question>();

        public string sToken = "e1Rqn1wMZHUxSygGz8DuiY3K9L37WSn";
        public string sCorpID = "wwf0b1680d1b942f7b";
        public string sEncodingAESKey = "qKN5jIoXJOHw7Wp5WzowrsuQVVr4qkjhU2vUPaciBtE";
        public override void handleGETRequest(HttpProcessor p)
        {
            Console.WriteLine("request: {0}", p.http_url);
            //企业微信后台开发者设置的token, corpID, EncodingAESKey


            /*
                        ------------使用示例一：验证回调URL---------------
                        *企业开启回调模式时，企业微信会向验证url发送一个get请求 
                        假设点击验证时，企业收到类似请求：
                        * GET /cgi-bin/wxpush?msg_signature=5c45ff5e21c57e6ad56bac8758b79b1d9ac89fd3&timestamp=1409659589&nonce=263014780&echostr=P9nAzCzyDtyTWESHep1vC5X9xho%2FqYX3Zpb4yKa9SKld1DsH3Iyt3tP3zNdtp%2B4RPcs8TgAE7OaBO%2BFZXvnaqQ%3D%3D 
                        * HTTP/1.1 Host: qy.weixin.qq.com

                        * 接收到该请求时，企业应                        1.解析出Get请求的参数，包括消息体签名(msg_signature)，时间戳(timestamp)，随机数字串(nonce)以及企业微信推送过来的随机加密字符串(echostr),
                        这一步注意作URL解码。
                        2.验证消息体签名的正确性 
                        3.解密出echostr原文，将原文当作Get请求的response，返回给企业微信
                        第2，3步可以用企业微信提供的库函数VerifyURL来实现。
                        */

            Tencent.WXBizMsgCrypt wxcpt = new Tencent.WXBizMsgCrypt(sToken, sEncodingAESKey, sCorpID);
            string all_str = p.http_url;
            all_str = all_str.Substring(all_str.IndexOf("?") + 1);
            string[] parm = all_str.Split('&');
            // string sVerifyMsgSig = HttpUtils.ParseUrl("msg_signature");

            string sVerifyMsgSig = HttpUtility.UrlDecode(parm[0].Substring(parm[0].IndexOf("=") + 1));
            // string sVerifyTimeStamp = HttpUtils.ParseUrl("timestamp");
            string sVerifyTimeStamp = HttpUtility.UrlDecode(parm[1].Substring(parm[1].IndexOf("=") + 1));
            // string sVerifyNonce = HttpUtils.ParseUrl("nonce");
            string sVerifyNonce = HttpUtility.UrlDecode(parm[2].Substring(parm[2].IndexOf("=") + 1));
            // string sVerifyEchoStr = HttpUtils.ParseUrl("echostr");
            string sVerifyEchoStr = HttpUtility.UrlDecode(parm[3].Substring(parm[3].IndexOf("=") + 1));
            int ret = 0;
            string sEchoStr = "";

            ret = wxcpt.VerifyURL(sVerifyMsgSig, sVerifyTimeStamp, sVerifyNonce, sVerifyEchoStr, ref sEchoStr);
            if (ret != 0)
            {
                System.Console.WriteLine("ERR: VerifyURL fail, ret: " + ret);
                return;
            }
            //ret==0表示验证成功，sEchoStr参数表示明文，用户需要将sEchoStr作为get请求的返回参数，返回给企业微信。
            // HttpUtils.SetResponse(sEchoStr);
            p.writeSuccess();
            p.outputStream.WriteLine(sEchoStr);


        }

        public override void handlePOSTRequest(HttpProcessor p, StreamReader inputData)
        {
            Console.WriteLine("POST request: {0}", p.http_url);
            string data = inputData.ReadToEnd();
            /*
            Tencent.WXBizMsgCrypt wxcpt = new Tencent.WXBizMsgCrypt(sToken, sEncodingAESKey, sCorpID);
            ------------使用示例二：对用户回复的消息解密-------------- -
            用户回复消息或者点击事件响应时，企业会收到回调消息，此消息是经过企业微信加密之后的密文以post形式发送给企业，密文格式请参考官方文档
            假设企业收到企业微信的回调消息如下：

            POST / cgi - bin / wxpush ? msg_signature = 477715d11cdb4164915debcba66cb864d751f3e6 & timestamp = 1409659813 & nonce = 1372623149 HTTP / 1.1

            Host: qy.weixin.qq.com

            Content - Length: 613
              < xml >           < ToUserName >< ![CDATA[wx5823bf96d3bd56c7]] ></ ToUserName >< Encrypt >< ![CDATA[RypEvHKD8QQKFhvQ6QleEB4J58tiPdvo + rtK1I9qca6aM / wvqnLSV5zEPeusUiX5L5X / 0lWfrf0QADHHhGd3QczcdCUpj911L3vg3W / sYYvuJTs3TUUkSUXxaccAS0qhxchrRYt66wiSpGLYL42aM6A8dTT + 6k4aSknmPj48kzJs8qLjvd4Xgpue06DOdnLxAUHzM6 + kDZ + HMZfJYuR + LtwGc2hgf5gsijff0ekUNXZiqATP7PF5mZxZ3Izoun1s4zG4LUMnvw2r + KqCKIw + 3IQH03v + BCA9nMELNqbSf6tiWSrXJB3LAVGUcallcrw8V2t9EL4EhzJWrQUax5wLVMNS0 + rUPA3k22Ncx4XXZS9o0MBH27Bo6BpNelZpS +/ uh9KsNlY6bHCmJU9p8g7m3fVKn28H3KDYA5Pl / T8Z1ptDAVe0lXdQ2YoyyH2uyPIGHBZZIs2pDBS8R07 + qN + E7Q ==]] ></ Encrypt >
  
              < AgentID >< ![CDATA[218]] ></ AgentID >
  
              </ xml >

              企业收到post请求之后应该          1.解析出url上的参数，包括消息体签名(msg_signature)，时间戳(timestamp)以及随机数字串(nonce)

            2.验证消息体签名的正确性。
			3.将post请求的数据进行xml解析，并将<Encrypt> 标签的内容进行解密，解密出来的明文即是用户回复消息的明文，明文格式请参考官方文档
             第2，3步可以用企业微信提供的库函数DecryptMsg来实现。
			*/
            Tencent.WXBizMsgCrypt wxcpt = new Tencent.WXBizMsgCrypt(sToken, sEncodingAESKey, sCorpID);
            string all_str = p.http_url;
            all_str = all_str.Substring(all_str.IndexOf("?") + 1);
            string[] parm = all_str.Split('&');

            // string sReqMsgSig = HttpUtils.ParseUrl("msg_signature");
            string sReqMsgSig = HttpUtility.UrlDecode(parm[0].Substring(parm[0].IndexOf("=") + 1));
            // string sReqTimeStamp = HttpUtils.ParseUrl("timestamp");
            string sReqTimeStamp = HttpUtility.UrlDecode(parm[1].Substring(parm[1].IndexOf("=") + 1));
            // string sReqNonce = HttpUtils.ParseUrl("nonce");
            string sReqNonce = HttpUtility.UrlDecode(parm[2].Substring(parm[2].IndexOf("=") + 1));
            // Post请求的密文数据
            // string sReqData = HttpUtils.PostData();
            string sReqData = data;
            string sMsg = "";  // 解析之后的明文
            int ret = wxcpt.DecryptMsg(sReqMsgSig, sReqTimeStamp, sReqNonce, sReqData, ref sMsg);
            if (ret != 0)
            {
                System.Console.WriteLine("ERR: Decrypt Fail, ret: " + ret);
                return;
            }
            Console.Write(sMsg);
            p.writeSuccess();
            // ret==0表示解密成功，sMsg表示解密之后的明文xml串
            // TODO: 对明文的处理
            // For example:
            XmlDocument doc = new XmlDocument();
            doc.LoadXml(sMsg);
            XmlNode root = doc.FirstChild;
            bool message_stat = false;
            if (root["MsgType"].InnerText == "text" && !message_stat)
            {
                string content = root["Content"].InnerText;
                string content_from = root["FromUserName"].InnerText;
                if (content.IndexOf("今天") > -1 && content.IndexOf("应班") > -1 && (content.IndexOf("?") > -1 || content.IndexOf("？") > -1))
                {
                    SqlConnection conn = new SqlConnection();
                    conn.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
                    conn.Open();
                    SqlCommand cmd = new SqlCommand();
                    cmd.Connection = conn;
                    cmd.CommandText = "select * from qywx_xxkpb where (pb=DATEDIFF(day,'2020-03-26',getdate())+1 and DATEPART(HOUR,getdate())>=8) or (pb=DATEDIFF(day,'2020-03-26',getdate()) and DATEPART(HOUR,getdate())<8)";
                    SqlDataReader read1 = cmd.ExecuteReader();
                    read1.Read();
                    string huifu = "";
                    if (read1["userid"].ToString() == content_from)
                    {
                        huifu = "问啥问？就是你!";
                    }
                    else
                    {
                        huifu = "今天是" + read1["name"].ToString() + "应班";
                    }
                    ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                    h1.App_send(content_from, "", "", huifu, "0");
                    message_stat = true;
                }

                if (qt.Count > 0 && !message_stat)
                {
                    //foreach (var i in qt)
                    for (int i = 0; i < qt.Count; i++)
                    {
                        if (DateTime.Now.Subtract(qt[i].lasttime).TotalSeconds > 180)
                        {
                            //qt.Remove(qt[i]);   //直接删除可能会影响循环，但需另定期清理过期元素。代码尚未添加 
                            //list批量删除元素
                        }
                        else
                        {
                            if (content_from == qt[i].user_id)
                            {
                                if (qt[i].title == "yingbanstat1")
                                {
                                    if ((content.Trim().Length == 10) || (content.Trim() == "今天") || (content.Trim() == "明天") || (content.Trim() == "后天") || (content.Trim() == "昨天"))
                                    {
                                        if (content.Trim() == "今天")
                                        {
                                            content = DateTime.Now.ToString("yyyy-MM-dd");
                                        }
                                        if (content.Trim() == "明天")
                                        {
                                            content = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
                                        }
                                        if (content.Trim() == "后天")
                                        {
                                            content = DateTime.Now.AddDays(2).ToString("yyyy-MM-dd");
                                        }
                                        if (content.Trim() == "昨天")
                                        {
                                            content = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd");
                                        }
                                        SqlConnection conn = new SqlConnection();
                                        conn.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
                                        conn.Open();
                                        SqlCommand cmd = new SqlCommand();
                                        cmd.Connection = conn;
                                        cmd.CommandText = string.Format("select * from qywx_xxkpb where (pb=DATEDIFF(day,'2020-03-26','{0}')%6+1 )", content);
                                        try
                                        {
                                            SqlDataReader read1 = cmd.ExecuteReader();
                                            read1.Read();
                                            string huifu = "";
                                            if (read1["userid"].ToString() == content_from)
                                            {
                                                huifu = "问啥问？就是你!";
                                            }
                                            else
                                            {
                                                huifu = content + "是" + read1["name"].ToString() + "应班";
                                            }
                                            ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                                            h1.App_send(content_from, "", "", huifu, "0");
                                            message_stat = true;
                                        }
                                        catch (Exception)
                                        {

                                            return;
                                        }
                                        finally
                                        {
                                            conn.Close();
                                        }
                                        qt.Remove(qt[i]);
                                    }
                                    else
                                    {
                                        ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                                        h1.App_send(content_from, "", "", "请注意输入日期格式例如：（2020-01-01）", "0");
                                    }
                                }
                                if (qt[i].title == "txl_qry")
                                {
                                    SqlConnection conn = new SqlConnection();
                                    conn.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
                                    conn.Open();
                                    SqlCommand cmd = new SqlCommand();
                                    cmd.Connection = conn;
                                    string sqltext = "";
                                    if (content.IndexOf("姓名") == 0 || content.IndexOf("单位") == 0 || content.IndexOf("标签") == 0)
                                    {
                                        if (content.IndexOf("姓名") == 0)
                                        {
                                            sqltext = string.Format(" name like '%{0}%' ", content.Substring(3));
                                        }
                                        if (content.IndexOf("单位") == 0)
                                        {
                                            sqltext = string.Format(" unit like '%{0}%' ", content.Substring(3));
                                        }
                                        if (content.IndexOf("标签") == 0)
                                        {
                                            sqltext = string.Format(" label like '%{0}%' ", content.Substring(3));
                                        }
                                        cmd.CommandText = string.Format("select * from qywx_phone where {0}", sqltext);
                                        SqlDataReader rder = cmd.ExecuteReader();
                                        ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                                        if (!rder.HasRows)
                                        {
                                            h1.App_send(content_from, "", "", "未查询相关通讯录！", "0");
                                        }
                                        else
                                        {
                                            string jg = "";
                                            while (rder.Read())
                                            {
                                                jg = jg + string.Format("姓名：{0}; 单位：{1}; 电话：{2} \n", rder["name"].ToString(), rder["unit"].ToString(), rder["phone"].ToString());
                                            }
                                            h1.App_send(content_from, "", "", jg, "0");

                                        }
                                        qt.Remove(qt[i]);
                                    }
                                    else
                                    {
                                        ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                                        h1.App_send(content_from, "", "", "请注意输入查询格式如：（姓名@小明）", "0");
                                    }

                                }
                            }
                        }


                    }
                }


                if (content == "小明小明")
                {


                    ConsoleApplication1.Http_send_cardmessage msg = new ConsoleApplication1.Http_send_cardmessage();
                    msg.title = "闹啥呢？";
                    msg.description = "请谨慎选择！！！";
                    msg.task_id = "ASK" + Convert.ToInt64((DateTime.Now - new DateTime(1970, 1, 1, 0, 0, 0, 0)).TotalSeconds).ToString();
                    msg.btn = new ConsoleApplication1.Http_send_cardmessage.Message_btn[2];
                    msg.btn[0] = new ConsoleApplication1.Http_send_cardmessage.Message_btn();
                    msg.btn[0].key = "btn0";
                    msg.btn[0].name = "查询应班情况";
                    msg.btn[0].replace_name = "查询应班情况";
                    msg.btn[0].btncolor = ConsoleApplication1.Http_send_cardmessage.Message_btn.color.blue;
                    msg.btn[0].bold = true;
                    msg.btn[1] = new ConsoleApplication1.Http_send_cardmessage.Message_btn();
                    msg.btn[1].key = "btn1";
                    msg.btn[1].name = "通讯录查询";
                    msg.btn[1].replace_name = "通讯录查询";
                    msg.btn[1].btncolor = ConsoleApplication1.Http_send_cardmessage.Message_btn.color.blue;
                    msg.btn[1].bold = true;
                    ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();
                    h1.App_send_card(content_from, "", "", msg);

                }
            }
            else
            {
                if (root["MsgType"].InnerText == "event")
                {
                    if (root["EventKey"].InnerText == "btn0")
                    {
                        ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();

                        h1.App_send(root["FromUserName"].InnerText, "", "", "请问查几号应班情况？请输入日期（yyyy-mm-dd）", "0");
                        for (int i = 0; i < qt.Count; i++)
                        {
                            if (qt[i].user_id == root["FromUserName"].InnerText)
                            {
                                qt.Remove(qt[i]);
                                break;
                            }
                        }
                        Question tmp = new Question();
                        tmp.title = "yingbanstat1";
                        tmp.user_id = root["FromUserName"].InnerText;
                        tmp.lasttime = DateTime.Now;
                        qt.Add(tmp);
                    }
                    if (root["EventKey"].InnerText == "btn1")
                    {
                        ConsoleApplication1.Http_send h1 = new ConsoleApplication1.Http_send();

                        h1.App_send(root["FromUserName"].InnerText, "", "", "请输入查询类容？格式（姓名@XXX\\单位@XXX\\标签@XXX）", "0");
                        for (int i = 0; i < qt.Count; i++)
                        {
                            if (qt[i].user_id == root["FromUserName"].InnerText)
                            {
                                qt.Remove(qt[i]);
                                break;
                            }
                        }
                        Question tmp = new Question();
                        tmp.title = "txl_qry";
                        tmp.user_id = root["FromUserName"].InnerText;
                        tmp.lasttime = DateTime.Now;
                        qt.Add(tmp);
                    }
                }
            }



        }
    }

    public class TestMain
    {
        public static int Main(String[] args)
        {


            HttpServer httpServer;
            if (args.GetLength(0) > 0)
            {
                httpServer = new MyHttpServer(Convert.ToInt16(args[0]));
            }
            else
            {
                httpServer = new MyHttpServer(4366);
            }
            Thread thread = new Thread(new ThreadStart(httpServer.listen));
            thread.Start();

            System.Timers.Timer timer1 = new System.Timers.Timer(600000);
            timer1.Elapsed += new System.Timers.ElapsedEventHandler(xxsend);
            timer1.AutoReset = true;
            timer1.Start();
            return 0;
        }


        public static Boolean zxstat = false;
        public static void xxsend(object source, System.Timers.ElapsedEventArgs e)
        {
            string tmp = DateTime.Now.Hour.ToString();
            if ((!zxstat) && (DateTime.Now.Hour.ToString() == "8"))
            {
                ConsoleApplication1.Http_send user_send = new ConsoleApplication1.Http_send();
                SqlConnection connt = new SqlConnection();
                connt.ConnectionString = "server=172.22.52.51;database=master;user=snzyy;pwd=Snzyy123.";
                connt.Open();
                SqlCommand sqlstr = new SqlCommand();
                sqlstr.Connection = connt;
                sqlstr.CommandType = System.Data.CommandType.Text;
                //string sqltext = "SELECT SUM(aa.住院量) AS 住院量,SUM(aa.门诊量)-SUM(aa.急诊量) AS 门诊量,SUM(aa.急诊量) AS 急诊量,SUM(aa.手术量) AS 手术量,SUM(aa.预约挂号数) AS 预约挂号数 ";
                //sqltext = sqltext + "FROM (SELECT count(cc.住院量) AS 住院量,0 AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数 FROM (SELECT MAX(a.PAT_IN_HOS_ID) AS 住院量 FROM zy.[IN].PAT_ALL_INFO_VIEW a ";
                //sqltext = sqltext + string.Format("LEFT JOIN ZY.[IN].BEDS b ON b.BED_ID = a.BED_ID WHERE IN_HOS_STATUS = 1 AND b.BED_STATUS = 1 AND a.PAT_IN_TIME < '{0}' AND a.PAT_LEAVE_ORDER_LEAVE_TIME < {1} ", DateTime.Now.AddDays(-1).ToShortDateString()+" 00:00:00", DateTime.Now.AddDays(-1).ToShortDateString()+" 23:59:59");
                string sqltext = string.Format("SELECT SUM(aa.住院量) AS 住院量,SUM(aa.门诊量)-SUM(aa.急诊量) AS 门诊量,SUM(aa.急诊量) AS 急诊量,SUM(aa.手术量) AS 手术量,SUM(aa.预约挂号数) AS 预约挂号数,SUM(aa.今日出院) 今日出院,SUM(aa.今日入院) 今日入院 FROM (SELECT count(PAT_IN_HOS_CODE) AS 住院量,0 AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数,0 as 今日出院,0 as 今日入院 from zy.[IN].PAT_ALL_INFO_VIEW where PAT_IN_HOS_CODE<>'' and  (CONVERT(VARCHAR(18),PAT_IN_TIME,120) = CONVERT(VARCHAR(18),PAT_LEAVE_ORDER_LEAVE_TIME,120) or PAT_LEAVE_ORDER_LEAVE_TIME > '{1}')  and PAT_IN_TIME<'{1}' UNION ALL SELECT 0 as 住院量,COUNT(*) AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数,0 as 今日出院,0 as 今日入院 FROM MZ.OUT.REGISTERS WHERE CREATE_TIME > '{0}' AND CREATE_TIME < '{1}' UNION ALL SELECT 0 as 住院量,0 AS 门诊量,SUM(bb.急诊量) AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数,0 as 今日出院,0 as 今日入院 FROM (SELECT COUNT(*) 急诊量 FROM mz.out.REGISTERS a LEFT JOIN comm.comm.DEPTS b ON b.DEPT_ID = a.DEPT_ID LEFT JOIN COMM.COMM.USERINFO_VIEW d WITH ( NOLOCK ) ON a.DOC_ID = d.USER_SYS_ID LEFT  JOIN mz.OUT.DOC_REGISTER e ON a.REGISTER_ID=e.REGISTER_ID WHERE a.CREATE_TIME >= '{0}' AND a.CREATE_TIME <= '{1}' AND a.DEPT_id IN (160,185) AND d.UESR_NAME IS not NULL AND a.FLAG_INVALID = 0 AND a.REGISTER_STATUS = 5 UNION ALL SELECT  COUNT(a.PAT_IN_HOS_ID) 急诊量 FROM zy.[IN].PAT_ALL_INFO_VIEW a LEFT JOIN comm.COMM.USERINFO_VIEW d ON a.OUT_DOC_ID=d.USER_SYS_ID LEFT JOIN COMM.COMM.DEPTS c ON c.DEPT_ID = d.DEPT_ID WHERE 1=1  AND a.PAT_IN_TIME >='{0}' AND a.PAT_IN_TIME <='{1}'  AND c.DEPT_ID IN (160,185)) bb UNION ALL SELECT 0 as 住院量,0 AS 门诊量, 0 AS 急诊量, COUNT(*) AS 手术量 ,0 AS 预约挂号数,0 as 今日出院,0 as 今日入院 FROM oas4.dbo.OAS_PATIENT_EVENT_DATA WHERE event_begintime > '{0}' AND event_begintime < '{1}' AND event_name = '手术开始' UNION ALL SELECT 0 as 住院量,0 AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,COUNT(*) AS 预约挂号数,0 as 今日出院,0 as 今日入院 FROM ARS.dbo.APPOINTMENT_REAL_REGISTER WHERE REGISTER_TIME2 >'{0}' AND REGISTER_TIME2 <'{1}' AND INVALID <> 1 union all SELECT 0 AS 住院量,0 AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数,count(PAT_IN_HOS_ID) as 今日出院,0 as 今日入院 FROM zy.[IN].PAT_ALL_INFO_VIEW a WHERE a.PAT_LEAVE_ORDER_LEAVE_TIME > '{0}' AND a.PAT_LEAVE_ORDER_LEAVE_TIME < '{1}' and CONVERT(VARCHAR(18),PAT_IN_TIME,120) <> CONVERT(VARCHAR(18),PAT_LEAVE_ORDER_LEAVE_TIME,120) union all select  0 AS 住院量,0 AS 门诊量, 0 AS 急诊量, 0 AS 手术量 ,0 AS 预约挂号数,0 as 今日出院,count(*) 今日入院 from zy.[IN].PAT_ALL_INFO_VIEW a where PAT_IN_TIME > '{0}' AND PAT_IN_TIME < '{1}' and PAT_IN_HOS_CODE<>'' and FLAG_INVALID=0) aa", DateTime.Now.AddDays(-1).ToShortDateString() + " 08:00:00", DateTime.Now.ToShortDateString() + " 08:00:00");
                sqlstr.CommandText = sqltext;
                SqlDataReader rder = sqlstr.ExecuteReader();
                rder.Read();


                string wbtext = DateTime.Now.AddDays(-1).ToLongDateString() + "8点至" + DateTime.Now.ToLongDateString() + "8点" + "\n" + string.Format("门诊量：{0}；预约挂号：{1}；急诊量：{2}；住院量：{3}；手术量：{4}；当日出院：{5}；当日入院：{6}", rder["门诊量"].ToString(), rder["预约挂号数"].ToString(), rder["急诊量"].ToString(), rder["住院量"].ToString(), rder["手术量"].ToString(), rder["今日出院"].ToString(), rder["今日入院"].ToString());

                string x_huizong = wbtext + "\n";





                rder.Close();

                string tx_sr;
                tx_sr = DateTime.Now.AddDays(-1).ToLongDateString() + "\n";
                sqltext = string.Format("SELECT cast (round(SUM(金额)/10000,2) as numeric(8,2)) AS 总金额 FROM(SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_CHARGE AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_MED AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT  SUM(ROUND(AMOUNT,2))  金额 FROM ZY.[IN].IN_BILL_RECORD B WITH(NOLOCK) WHERE 1=1 AND b.create_time > '{0}' AND b.create_time < '{1}') MM", DateTime.Now.AddDays(-1).ToShortDateString() + " 00:00:00", DateTime.Now.AddDays(-1).ToShortDateString() + " 23:59:59");
                sqlstr.CommandText = sqltext;
                rder = sqlstr.ExecuteReader();
                rder.Read();

                tx_sr = tx_sr + "当日收入:" + rder["总金额"].ToString() + "万元\n";
                rder.Close();

                sqltext = string.Format("SELECT cast (round(SUM(金额)/10000,2) as numeric(8,2)) AS 总金额 FROM(SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_CHARGE AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_MED AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT  SUM(ROUND(AMOUNT,2))  金额 FROM ZY.[IN].IN_BILL_RECORD B WITH(NOLOCK) WHERE 1=1 AND b.create_time > '{0}' AND b.create_time < '{1}') MM", DateTime.Now.Year.ToString() + "-" + DateTime.Now.Month.ToString() + "-" + "01" + " 00:00:00", DateTime.Now.AddDays(-1).ToShortDateString() + " 23:59:59");
                sqlstr.CommandText = sqltext;
                rder = sqlstr.ExecuteReader();
                rder.Read();

                tx_sr = tx_sr + "当月收入:" + rder["总金额"].ToString() + "万元\n";
                rder.Close();

                sqltext = string.Format("SELECT cast (round(SUM(金额)/10000,2) as numeric(8,2)) AS 总金额 FROM(SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_CHARGE AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT SUM(b.GET_AMOUNT)AS 金额 FROM MZ.OUT.INVOICE_MAIN AS A WITH(NOLOCK) INNER JOIN MZ.OUT.INVOICE_DETAILS_MED AS B WITH(NOLOCK) ON A.INVOICE_ID = B.INVOICE_ID WHERE 1=1 AND a.create_time > '{0}' AND a.create_time < '{1}' UNION ALL SELECT  SUM(ROUND(AMOUNT,2))  金额 FROM ZY.[IN].IN_BILL_RECORD B WITH(NOLOCK) WHERE 1=1 AND b.create_time > '{0}' AND b.create_time < '{1}') MM", DateTime.Now.Year.ToString() + "-" + "01" + "-" + "01" + " 00:00:00", DateTime.Now.AddDays(-1).ToShortDateString() + " 23:59:59");
                sqlstr.CommandText = sqltext;
                rder = sqlstr.ExecuteReader();
                rder.Read();

                tx_sr = tx_sr + "本年至今:" + rder["总金额"].ToString() + "万元";
                rder.Close();


                user_send.App_send("", "2", "1", tx_sr);

                connt.Close();

                SqlConnection connt_old = new SqlConnection();
                connt_old.ConnectionString = "server=192.168.8.18;database=my_data;user=sa;pwd=VA4X1abfy76pY";
                connt_old.Open();
                SqlCommand sqlstr_old = new SqlCommand();
                sqlstr_old.Connection = connt_old;
                sqlstr_old.CommandType = System.Data.CommandType.Text;
                sqltext = string.Format("select sum(总门诊量)-sum(急诊量) 门诊量,sum(急诊量) 急诊量,sum(住院量) 住院量,sum(当日出院) 当日出院,sum(当日入院) 当日入院 from (select count(*) 总门诊量,0 急诊量,0 住院量,0 当日出院,0 当日入院 from (select distinct RTRIM(brxm) brxm,RTRIM(czks) ksys  from dzbl_brbl_last where zdrq between '{0}' and '{1}' and tmh not in (select tmh from mzsf_mzfymx where sfrq between '{0}' and '{1}') union all select distinct RTRIM(brxm) brxm,RTRIM(kdks)  ksys from mzsf_mzfymx where sfrq between '{0}' and '{1}' union all select distinct RTRIM(brxm) brxm,RTRIM(kdks) ksys from mzsf_mzfymx_bf where sfrq between '{0}' and '{1}') xa union all select 0 总门诊量,count(*) 急诊量,0 住院量,0 当日出院,0 当日入院 from (select distinct rtrim(tmh)+convert(char,sfrq,112) brbs from mzsf_mzfymx where sfrq between '{0}' and '{1}' and tfbz=0 and zfbz=0 and (kdks in (select dm from sys_ksdm where mc like '%急诊%') or czks in (select dm from sys_ksdm where mc like '%急诊%')) union all select distinct rtrim(tmh)+convert(char,sfrq,112) brbs from mzsf_mzfymx_bf where sfrq between '{0}' and '{1}' and tfbz=0 and zfbz=0 and (kdks in (select dm from sys_ksdm where mc like '%急诊%') or czks in (select dm from sys_ksdm where mc like '%急诊%')) union all select distinct rtrim(tmh)+convert(char,sfrq,112) brbs from mzsf_mzfymx_zc where sfrq between '{0}' and '{1}' and tfbz=0 and zfbz=0 and (kdks in (select dm from sys_ksdm where mc like '%急诊%') or czks in (select dm from sys_ksdm where mc like '%急诊%')) ) xb union all select 0 总门诊量,0 急诊量,count(*) 住院量,0 当日出院,0 当日入院 from ( select tmh from zysf_zydj where ryrq<'{1}' and (cybz=0 or cyrq>'{1}') union all select tmh from zysf_cydj where ryrq<'{1}' and (cybz=0 or cyrq>'{1}') union all select tmh from zyzc_zysf_cydj where ryrq<'{1}' and (cybz=0 or cyrq>'{1}') ) xc union all select 0 总门诊量,0 急诊量,0 住院量,count(*) 当日出院,0 当日入院 from ( select tmh from zysf_zydj where cybz=1 and cyrq  between '{0}' and '{1}' union all select tmh from zysf_cydj where cybz=1 and cyrq  between '{0}' and '{1}' union all select tmh from zyzc_zysf_cydj where cybz=1 and cyrq  between '{0}' and '{1}' ) xd union all select 0 总门诊量,0 急诊量,0 住院量,0 当日出院,count(*) 当日入院  from (select zyh from zysf_zydj where ryrq between'{0}' and '{1}' union all select zyh from zysf_cydj where ryrq between'{0}' and '{1}' union all select zyh from zyzc_zysf_cydj where ryrq between'{0}' and '{1}') xe) xxa", DateTime.Now.AddDays(-1).AddYears(-1).ToShortDateString() + " 08:00:00", DateTime.Now.AddYears(-1).ToShortDateString() + " 08:00:00");
                sqlstr_old.CommandText = sqltext;
                SqlDataReader rder_old = sqlstr_old.ExecuteReader();
                rder_old.Read();
                wbtext = DateTime.Now.AddYears(-1).AddDays(-1).ToLongDateString() + "8点至" + DateTime.Now.AddYears(-1).ToLongDateString() + "8点" + "\n" + string.Format("门诊量：{0}；急诊量：{1}；住院量：{2}；当日出院：{3}；当日入院：{4}", rder_old["门诊量"].ToString(), rder_old["急诊量"].ToString(), rder_old["住院量"].ToString(), rder_old["当日出院"].ToString(), rder_old["当日入院"].ToString());

                x_huizong = x_huizong + wbtext;
                user_send.App_send("", "2", "", x_huizong);
                user_send.App_send_group("wrLGxDBgAA5Hdw_d2rY7cRhd3hOg4Jsg", x_huizong);

                rder_old.Close();
                connt_old.Close();
                zxstat = true;
            }
            else
            {
                if (DateTime.Now.Hour.ToString() != "8")
                {
                    zxstat = false;
                }
            }
        }

    }

}
