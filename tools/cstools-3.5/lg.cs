using System;
using System.IO;
using System.Text;
using System.Collections;
using Tools;

public class LexerGenerate : TokensGen // used during LexerGenerate
{
	public void Emit(Hashtable actions,string actvars,bool nameSpace,bool showDfa) 
	{
		if (showDfa) 
		{
			for (int j=0;j<states.Count; j++)
				((Dfa)states[j]).Print(Console.Out);
		}
		m_outFile.WriteLine("public class yy"+m_outname+" : Tokens {");
		m_outFile.WriteLine(" public yy"+m_outname+"() { arr = new byte[] { ");
		m_tokens.EmitDfa(m_outFile);
		foreach(string x in m_tokens.tokens.Keys)
			m_outFile.WriteLine(" new Tfactory(this,\""+x+"\",new TCreator("+x+"_factory));");
		m_outFile.WriteLine("}");
		foreach (string y in m_tokens.tokens.Keys)
			m_outFile.WriteLine("public static object "+y+"_factory(Lexer yyl) { return new "+y+"(yyl);}");
		Console.WriteLine("Actions function");
		m_outFile.WriteLine("public override TOKEN OldAction(Lexer yym,string yytext, int action, ref bool reject) {");
		m_outFile.WriteLine("  switch(action) {");
		m_outFile.WriteLine("  case -1: break;");
		IDictionaryEnumerator pos = actions.GetEnumerator();
		for (int m=0;m<actions.Count;m++) 
		{
			pos.MoveNext();
			int act = (int)pos.Key;
			NfaNode e = (NfaNode)pos.Value;
			if (e.m_sTerminal.Length!=0 && e.m_sTerminal[0]=='%') // auto token action
				continue;
			m_outFile.WriteLine("   case {0}: {1}",act,ActionTransform(e.m_sTerminal));
			m_outFile.WriteLine("      break;"); // in case m_sTerminal ends with a // comment (quite likely)
		}	
		m_outFile.WriteLine("  }");
		m_outFile.WriteLine("  return null;");
		m_outFile.WriteLine("}}");
		m_outFile.WriteLine("public class "+m_outname+":Lexer {");
		m_outFile.WriteLine("public "+m_outname+"():base(new yy"+m_outname+"()) {}");
		m_outFile.WriteLine("public "+m_outname+"(Tokens tks):base(tks){}");
		m_outFile.WriteLine(actvars);
		m_outFile.WriteLine(" }");
		if (nameSpace)
			m_outFile.WriteLine("}");
		m_outFile.Close();
	}
	// This routine modifies Actions containing "return new MyToken()" to "return new MyToken(yyl)", etc
	// (version 3.0 addition, for thread safety purposes)
	string ActionTransform(string str)
	{
		string ret = "";
		while (str!="") 
		{
			int a = str.IndexOf("new");
			if (a<0)
				break;
			NonWhite(str,ref a,str.Length);
			White(str,ref a,str.Length);
			int b = a;
			while (char.IsLetter(str[b])||char.IsDigit(str[b])||str[b]=='_' && b<str.Length)
				b++;
			string cls = str.Substring(a,b-a);
			if (!m_tokens.tokens.Contains(cls)&& cls.CompareTo("TOKEN")!=0) 
			{
				ret += str.Substring(0,b);
				str = str.Substring(b);
			} 
			else 
			{
				a = str.IndexOf("(",b,str.Length-b);
				if (a<0)
					break;
				White(str,ref a,str.Length);
				a++;
				ret += str.Substring(0,a)+"yym";
				if (str[a]!=')') 
					ret += ",";
				str = str.Substring(a);
			}
		}
		return FixActions(ret+str);
	}
	public bool m_lexerseen = false;
	CsReader m_inFile; // the input script
	Hashtable m_actions = new Hashtable(); // int -> NfaNode
	Hashtable m_startstates = new Hashtable(); // string -> NfaNode
	string m_actvars = "";
	bool m_namespace = false;
	LineManager m_LineManager = new LineManager();

	bool OpenFiles(string fname,string bas) 
	{
		m_outname=bas;
		try 
		{
			m_inFile = new CsReader(fname);
		} 
		catch (Exception e) 
		{
			m_tokens.Error(String.Format("could not open {0} for reading [{1}]",fname,e.Message));
			return false;
		}
		try 
		{
			m_outFile = new StreamWriter(bas+".cs",false);
		} 
		catch (Exception e) 
		{
			m_tokens.Error(String.Format("could not open {0}.cs for writing [{1}]",bas,e.Message));
			return false;
		}
		m_outFile.WriteLine("using System;using Tools;");
		return true;
	}
	void CopyCode() 
	{
		for(;;) 
		{ // each line
			int c = m_inFile.Read();
			if (c=='%') 
			{
				int d = m_inFile.Read();
				if (d=='}')
					return;
				m_outFile.Write((char)c);
				c = d;
			} 
			while (c!='\n') 
			{
				m_outFile.Write((char)c);
				c = m_inFile.Read();
			}
			m_outFile.WriteLine();
		}
	}
	void GetRegex(string b, ref int p,int max) 
	{
		bool brack = false;
		int quote = 0;
		for (;p<max;p++)
			if (b[p]=='\\')
				p++;
			else if (quote==0 && b[p]=='[')
				brack = true;
			else if (brack && b[p]==']')
				brack = false;
			else if (b[p]==quote)
				quote = 0;
			else if (!brack && quote==0 && (b[p]=='\'' || b[p]=='"'))
				quote = b[p];
			else if (!brack && quote==0 && (b[p]==' '||b[p]=='\t'))
				break;
	}

	string NewConstructor(TokClassDef pT, string str) 
	{ 
		if (str=="")
			return "";
		string bas = pT.m_name;
		string newname = "";
		char[] buf = new char[32];
		for (int variant=1;;variant++) 
		{ // ensure we get a unique name
			newname = String.Format("{0}_{1}",bas,variant); 
			object o = m_tokens.tokens[newname];
			if (o==null)
				break;
		}
		pT = new TokClassDef(this,newname,bas);
		m_outFile.WriteLine("[Serializable] public class "+newname+" : "+bas+" {");
		m_outFile.Write("  public "+newname+"(Lexer yym):base(yym) ");
		pT.m_initialisation = FixActions(str);
		m_outFile.WriteLine(str + "}");
		return newname;
	}
	public void Create(string fname,string bas) 
	{
		m_tokens = new Tokens();
		string buf="";
		string str="";
		string name="";
		string startsym;
		Nfa nfa;
		int p,q,max;
		if (!OpenFiles(fname,bas))
			return;
		Console.WriteLine("Reading Input File"); Console.Out.Flush();
		while (!m_inFile.Eof()) 
		{
			buf = m_inFile.ReadLine();
			startsym = "YYINITIAL";
			max = buf.Length;
			p = 0;
			if (!White(buf,ref p,max))
				continue;
			if (buf[p]=='%') 
			{ // directive
				// %lexer
				if(buf.Length>=p+6 && "%lexer".Equals(buf.Substring(p,6))) 
				{
					m_lexerseen = true;
					continue;
				}
				// %encoding
				if (buf.Length>=p+9 && "%encoding".Equals(buf.Substring(p,9)))
				{
					p+=9; White(buf,ref p, max);
					q = p;
					NonWhite(buf,ref p, max);
					switch (buf.Substring(q,p-q))
					{
						case "ASCII": m_tokens.m_encoding = new ASCIIEncoding(); break;
						case "UTF7": m_tokens.m_encoding = new UTF7Encoding(); break;
						case "UTF8": m_tokens.m_encoding = new UTF8Encoding(); break;
						case "Unicode": m_tokens.m_encoding = new UnicodeEncoding(); break;
						default: Console.WriteLine("Warning: Encoding "+buf.Substring(q,p-q)+" unknown: ignored"); break;
					}
					continue;
				}
				// %namespace
				if (buf.Length>=p+10 && "%namespace".Equals(buf.Substring(p,10))) 
				{
					p+=10; White(buf,ref p,max);
					q = p;
					NonWhite(buf,ref p,max);
					m_outFile.WriteLine("namespace "+buf.Substring(q,p-q)+" {");
					m_namespace = true;
					continue;
				}
				// %define
				if(buf.Length>=p+7 && "%define".Equals(buf.Substring(p,7))) 
				{
					p+=7; White(buf,ref p,max);
					q = p;
					if (!NonWhite(buf,ref p,max)) 
					{
						Console.WriteLine("Bad define");
						continue;
					}
					name=buf.Substring(q,p-q);
					p++;
					if (White(buf,ref p,max))
						defines[name]=buf.Substring(p,max-p);
				} 
				else
					// % token/node
					if (buf.Length>=p+6 && "%token".Equals(buf.Substring(p,6)))
					EmitClassDefin(buf,ref p,max,m_inFile,"TOKEN", out str,out name,true);
				else if (buf.Length>=p+5 && "%node".Equals(buf.Substring(p,5)))
					EmitClassDefin(buf,ref p,max,m_inFile,"NODE",out str,out name,true);
				else if (buf.Length>=p+2 && "%{".Equals(buf.Substring(p,2)))
					CopyCode();
				else if (buf.Length>=p+9 && "%declare{".Equals(buf.Substring(p,9))) 
				{
					p += 8;
					m_actvars = ToBraceIfFound(ref buf,ref p,ref max,m_inFile);
					m_actvars = m_actvars.Substring(1,m_actvars.Length-2);
				} 
				else
					m_tokens.Error("Unknown directive "+buf.Substring(p,max-p));
				continue;
			} 
			else if (buf[p]=='<') 
			{  // startstate
				q = p++;
				while (p<max && buf[p]!='>')
					p++;
				if (p++ ==max) 
				{
					m_tokens.Error("Bad startsymbol");
					continue;
				}
				startsym = buf.Substring(q+1,p-q-2);
				White(buf, ref p, max);
			}
			q=p; // can't simply look for nonwhite space here because embedded spaces
			GetRegex(buf,ref p,max);
			Regex rgx = new Regex(this,buf.Substring(q,p-q));
			Nfa nfa1= new Nfa(this,rgx);
			if (!m_startstates.Contains(startsym))
				m_startstates[startsym] = new Nfa(this);
			nfa = (Nfa)m_startstates[startsym];
			nfa.AddEps(nfa1);
			White(buf,ref p,max);
			m_actions[nfa1.m_end.m_state] = nfa1.m_end;
			// handle multiline actions enclosed in {}
			nfa1.m_end.m_sTerminal = ToBraceIfFound(ref buf,ref p, ref max,m_inFile);
			// examine action string
			if (nfa1.m_end.m_sTerminal.Length>0 && nfa1.m_end.m_sTerminal[0] == '%') 
			{
				string tokClass,b = nfa1.m_end.m_sTerminal;
				q = 1;
				max = b.Length;
				int n;
				for (n=0;q<max&&b[q]!=' '&&b[q]!='\t'&&b[q]!='\n'&&b[q]!='{'&&b[q]!=':';q++,n++) // extract the class name
					;
				tokClass = b.Substring(1,n); // new-style auto token construction
				if (!("TOKEN".Equals(tokClass))) 
				{ // TOKEN is a special case
					object ob = m_tokens.tokens[tokClass];
					TokClassDef t = (TokClassDef)ob;
					bool isNew = (t==null);
					// check for initialisation action following %name
					string init = b.Substring(n+1,b.Length-n-1);
					string bas1 = "TOKEN";
					bool haveInit = false;
					for (int j=0;j<init.Length;j++)
						if (init[j]=='{') 
						{
							haveInit = true;
							break;
						} 
						else if (init[j]==':') 
						{
							bas1 = "";
							for (;init[j]==' '||init[j]=='\r';j++)
								;
							for (;init[j]!=' '&&init[j]!='\t'&&init[j]!='{'&&init[j]!='\n';j++)
								bas1 += init[j];
							break;
						}
					if (isNew) 
					{ // this token class has not been declared. Do so now
						bool isNode = (m_tokens.tokens[bas1]!=null);
						t = new TokClassDef(this,tokClass,bas1); // updates TOKEN.tokens
						m_outFile.WriteLine("//%{0}",tokClass);
						m_outFile.Write("[Serializable] public class {0} : {1}",tokClass,bas1);
						m_outFile.WriteLine("{ public override string yyname() { return \""+tokClass+"\";}");
						m_outFile.WriteLine(" public "+tokClass+"(Lexer yyl):base(yyl) {}}");
					}
					if (haveInit) 
						nfa1.m_end.m_sTerminal = "%"+NewConstructor(t,init);
				}
			} 
		}
		if (!m_lexerseen)
			m_tokens.Error("No lexer directive detected: possibly incorrect text encoding?");
		Console.WriteLine("Constructing DFAs"); Console.Out.Flush();
		foreach (string s in m_startstates.Keys)
		{
			Dfa d = new Dfa((Nfa)m_startstates[s]);
			m_tokens.starts[s] = d;
			if (d.m_actions!=null)
				Console.WriteLine("Warning: This lexer script generates an infinite token stream on bad input");
		}
		Console.WriteLine("Output phase"); Console.Out.Flush();
		Emit(m_actions,m_actvars,m_namespace,m_showDfa);
		Console.WriteLine("End of Create"); Console.Out.Flush();
		if (((Dfa)(m_tokens.starts["YYINITIAL"])).m_actions!=null) // repeat the above warning
			Console.WriteLine("Warning: This lexer script generates an infinite token stream on bad input"); 
	}
	public static void Main(string[] argv) 
	{
		int argc = argv.Length;
		LexerGenerate lexer = new LexerGenerate();
		int j;
		for (j = 0; argc>0 && argv[j][0]=='-'; j++,argc--)
			switch(argv[j][1]) {
			case 'D': lexer.m_showDfa = true; break;
	//		case 'I': lexer.m_iFlag = argv[j].Substring(2); break;
			}
		if (argc==1)
			lexer.Create(argv[j],"tokens");
		else if (argc==2)
			lexer.Create(argv[j],argv[j+1]);
		else
			lexer.Create("test.lexer","tokens");
		Console.WriteLine("LexerGenerator completed successfully");
	}
}
