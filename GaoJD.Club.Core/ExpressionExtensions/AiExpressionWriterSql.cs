﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// This source code is made available under the terms of the Microsoft Public License (MS-PL)

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace GaoJD.Club.Core
{

	internal enum AiExpSqlType : byte {
		aiWhere,
		aiOrder
	}
	/// <summary>
	/// 输出一个基于C#分析器的表达示树
	/// </summary>
	internal class AiExpressionWriterSql : AiExpressionVisitor {
		TextWriter writer;
		int indent = 2;
		int depth = 0;
		string aiWhereResult = string.Empty;
		string aiOrdeRsult = string.Empty;
		int aiOrderTime = 0;

		//表达式的左值，类似于p.Name==1的p.Name
		private Expression m_left;
		//二元表达式的类型，类似于p.Name==1的==
		private ExpressionType? m_binaryType;

		public int AiOrderTime {
			get { return aiOrderTime; }
			set {

				aiOrderTime = value;
			}
		}
		int bizWhereTime = 0;

		public int AiWhereTime {
			get { return bizWhereTime; }
			set {

				bizWhereTime = value;
			}
		}
		AiExpSqlType bizRead = AiExpSqlType.aiWhere;

		protected AiExpressionWriterSql(TextWriter writer) {
			this.writer = writer;
		}

		private static void Write(TextWriter writer, Expression expression) {
			new AiExpressionWriterSql(writer).Visit(expression);
		}

		private static string Write(TextWriter writer, Expression expression, AiExpSqlType bizSql) {
			expression = AiPartialEvaluator.Eval(expression);
			var bizR = new AiExpressionWriterSql(writer);
			bizR.bizRead = bizSql;
			bizR.Visit(expression);
			string result = string.Empty;
			switch (bizSql) {
				case AiExpSqlType.aiOrder:
					result = Regex.Replace(bizR.aiOrdeRsult, @",\s?$", "");
					return result;

				case AiExpSqlType.aiWhere:
					result = Regex.Replace(bizR.aiWhereResult, @"and\s?$", "");
					return result; ;
				default: return string.Empty;
			}

		}

		private static string WriteToString(Expression expression) {
			StringWriter sw = new StringWriter();
			Write(sw, expression);

			return sw.ToString();
		}

		public static string BizWhereWriteToString(Expression expression, AiExpSqlType bizSql) {
			StringWriter sw = new StringWriter();
			return Write(sw, expression, bizSql);
		}

		protected enum Indentation {
			Same,
			Inner,
			Outer
		}

		protected int IndentationWidth {
			get { return this.indent; }
			set { this.indent = value; }
		}

		protected void WriteLine(Indentation style) {
			this.writer.WriteLine();
			//  this.Indent(style);

		}

		private static readonly char[] splitters = new char[] { '\n', '\r' };

		protected void Write(string text) {
			switch (bizRead) {
				case AiExpSqlType.aiOrder: aiOrdeRsult = aiOrdeRsult + text; break;
				case AiExpSqlType.aiWhere: aiWhereResult = aiWhereResult + text; break;
			}

			this.writer.Write(text);
		}

		protected void Indent(Indentation style) {
			if (style == Indentation.Inner) {
				// this.depth++;
			} else if (style == Indentation.Outer) {
				// this.depth--;
				System.Diagnostics.Debug.Assert(this.depth >= 0);
			}
		}

		protected virtual string GetOperator(ExpressionType type) {
			switch (type) {
				case ExpressionType.Not:
					return "!";
				case ExpressionType.Add:
				case ExpressionType.AddChecked:
					return "+";
				case ExpressionType.Negate:
				case ExpressionType.NegateChecked:
				case ExpressionType.Subtract:
				case ExpressionType.SubtractChecked:
					return "-";
				case ExpressionType.Multiply:
				case ExpressionType.MultiplyChecked:
					return "*";
				case ExpressionType.Divide:
					return "/";
				case ExpressionType.Modulo:
					return "%";
				case ExpressionType.And:
					return "&";
				case ExpressionType.AndAlso:
					return "AND";
				case ExpressionType.Or:
					return "|";
				case ExpressionType.OrElse:
					return "OR";
				case ExpressionType.LessThan:
					return "<";
				case ExpressionType.LessThanOrEqual:
					return "<=";
				case ExpressionType.GreaterThan:
					return ">";
				case ExpressionType.GreaterThanOrEqual:
					return ">=";
				case ExpressionType.Equal:
					return "=";
				case ExpressionType.NotEqual:
					return "!=";
				case ExpressionType.Coalesce:
					return "??";
				case ExpressionType.RightShift:
					return ">>";
				case ExpressionType.LeftShift:
					return "<<";
				case ExpressionType.ExclusiveOr:
					return "^";
				default:
					return null;
			}
		}

		protected override Expression VisitBinary(BinaryExpression b) {
			this.Write("(");
			this.Visit(b.Left);
			this.Write(" ");
			this.Write(GetOperator(b.NodeType));
			m_binaryType = b.NodeType;
			this.Write(" ");
			this.Visit(b.Right);
			this.Write(")");
			return b;
		}

		protected Expression VisitBinary_Bak(BinaryExpression b) {
			this.Write("(");
			if (b.NodeType == ExpressionType.Power) {
				this.Write("POWER(");
				this.VisitValue(b.Left);
				this.Write(", ");
				this.VisitValue(b.Right);
				this.Write(")");
				//  return b;
			} else if (b.NodeType == ExpressionType.Coalesce) {
				this.Write("COALESCE(");
				this.VisitValue(b.Left);
				this.Write(", ");
				Expression right = b.Right;
				while (right.NodeType == ExpressionType.Coalesce) {
					BinaryExpression rb = (BinaryExpression)right;
					this.VisitValue(rb.Left);
					this.Write(", ");
					right = rb.Right;
				}
				this.VisitValue(right);
				this.Write(")");
				// return b;
			} else if (b.NodeType == ExpressionType.LeftShift) {
				this.Write("(");
				this.VisitValue(b.Left);
				this.Write(" * POWER(2, ");
				this.VisitValue(b.Right);
				this.Write("))");
				// return b;
			} else if (b.NodeType == ExpressionType.RightShift) {
				this.Write("(");
				this.VisitValue(b.Left);
				this.Write(" / POWER(2, ");
				this.VisitValue(b.Right);
				this.Write("))");
				//return b;
			} else {
				//if (b.Left is MemberExpression)
				//{
				//    if (((MemberExpression)b.Left).Type == typeof(bool) && (bizRead == BizExpSqlType.bizWhere))
				//    {
				//        this.Write("(" + ((MemberExpression)b.Left).Member.Name + " = 1)");
				//    }
				//    else
				//    {
				//        this.Visit(b.Left);
				//    }
				//}
				//else
				//{
				this.Visit(b.Left);
				//}

				this.Write(" ");
				this.Write(GetOperator(b.NodeType));
				m_binaryType = b.NodeType;
				this.Write(" ");
				this.Visit(b.Right);
			}
			this.Write(")");
			return b;
		}

		protected override Expression VisitUnary(UnaryExpression u) {
			switch (u.NodeType) {
				case ExpressionType.Convert:
				case ExpressionType.ConvertChecked:
					//this.Write("((");
					//this.Write(this.GetTypeName(u.Type));
					// this.Write(")");
					this.Visit(u.Operand);
					//this.Write(")");
					break;
				case ExpressionType.ArrayLength:
					this.Visit(u.Operand);
					this.Write(".Length");
					break;
				case ExpressionType.Quote:
					this.Visit(u.Operand);
					break;
				case ExpressionType.TypeAs:
					this.Visit(u.Operand);
					this.Write(" as ");
					this.Write(this.GetTypeName(u.Type));
					break;
				case ExpressionType.UnaryPlus:
					this.Visit(u.Operand);
					break;

				default:
					this.Write(this.GetOperator(u.NodeType));
					this.Visit(u.Operand);
					break;
			}
			return u;
		}

		protected virtual string GetTypeName(Type type) {
			string name = type.Name;
			name = name.Replace('+', '.');
			int iGeneneric = name.IndexOf('`');
			if (iGeneneric > 0) {
				name = name.Substring(0, iGeneneric);
			}
			if (type.IsGenericType || type.IsGenericTypeDefinition) {
				StringBuilder sb = new StringBuilder();
				sb.Append(name);
				sb.Append("<");
				var args = type.GetGenericArguments();
				for (int i = 0, n = args.Length; i < n; i++) {
					if (i > 0) {
						sb.Append(",");
					}
					if (type.IsGenericType) {
						sb.Append(this.GetTypeName(args[i]));
					}
				}
				sb.Append(">");
				name = sb.ToString();
			}
			return name;
		}

		protected override Expression VisitConditional(ConditionalExpression c) {
			this.Visit(c.Test);
			this.WriteLine(Indentation.Inner);
			this.Write("? ");
			this.Visit(c.IfTrue);
			this.WriteLine(Indentation.Same);
			this.Write(": ");
			this.Visit(c.IfFalse);
			this.Indent(Indentation.Outer);
			return c;
		}

		protected override IEnumerable<MemberBinding> VisitBindingList(ReadOnlyCollection<MemberBinding> original) {
			for (int i = 0, n = original.Count; i < n; i++) {
				this.VisitBinding(original[i]);
				if (i < n - 1) {
					this.Write(",");
					this.WriteLine(Indentation.Same);
				}
			}
			return original;
		}

		private static readonly char[] special = new char[] { '\n', '\n', '\\' };

		protected override Expression VisitConstant(ConstantExpression c) {
			//如果左值是访问成员，类似与p=>p.ID == 1
			if (m_left != null && m_left.NodeType == ExpressionType.MemberAccess) {
				if (c.Value == null) {
					this.Write("NULL");
					MemberExpression member = (MemberExpression)m_left;
					if (m_binaryType.HasValue) {
						switch (m_binaryType) {
							case ExpressionType.Equal:
								this.Write(string.Format(" OR [{0}] IS NULL", member.Member.Name));
								break;
							case ExpressionType.NotEqual:
								this.Write(string.Format(" OR [{0}] IS NOT NULL", member.Member.Name));
								break;
						}
						m_binaryType = null;
					}
				} else if (c.Type == typeof(DateTime) || c.Type == typeof(Guid)) {
					this.Write("'");//new DateTime(\" 
					this.Write(c.Value.ToString());
					this.Write("'");//\"
				} else if (c.Type.IsArray) {//如果value是数组
					Array array = (Array)c.Value;
					string str = string.Empty;
					foreach (var o in array) {
						switch (Type.GetTypeCode(o.GetType())) {
							case TypeCode.Boolean:
								str += (bool)o ? "1" : "0";
								break;
							case TypeCode.DateTime:
								str += string.Format("'{0}'", ((DateTime)o).ToString("yyyy-MM-dd HH:mm:ss"));
								break;
							case TypeCode.String:
							case TypeCode.Object:
								str += string.Format("'{0}'", o);
								break;
							default:
								str += o.ToString();
								break;
						}
						str += ",";
					}
					this.Write(str.TrimEnd(','));
				} else {
					switch (Type.GetTypeCode(c.Value.GetType())) {
						case TypeCode.Boolean:
							this.Write(((bool)c.Value) ? "1" : "0");
							break;
						case TypeCode.Single:
						case TypeCode.Double:
							string str = c.Value.ToString();
							if (!str.Contains('.')) {
								str += ".0";
							}
							this.Write(str);
							break;
						case TypeCode.DateTime:
							this.Write("'");//new DateTime(\"
							this.Write(c.Value.ToString());
							this.Write("'");//\"
							break;
						case TypeCode.String:
							this.Write("'");
							this.Write(c.Value.ToString().Replace("'", "\""));
							this.Write("'");
							break;
						default:
							this.Write(c.Value.ToString());
							break;
					}
				}
			} else {
				//如果存在p => true或者p => false
				if (c.Value.Equals(true)) {
					this.Write("1=1");
				} else if (c.Value.Equals(false)) {
					this.Write("1=0");
				}
			}
			m_left = null;
			return c;
		}

		protected override ElementInit VisitElementInitializer(ElementInit initializer) {
			if (initializer.Arguments.Count > 1) {
				this.Write("{");
				for (int i = 0, n = initializer.Arguments.Count; i < n; i++) {
					this.Visit(initializer.Arguments[i]);
					if (i < n - 1) {
						this.Write(", ");
					}
				}
				this.Write("}");
			} else {
				this.Visit(initializer.Arguments[0]);
			}
			return initializer;
		}

		protected override IEnumerable<ElementInit> VisitElementInitializerList(ReadOnlyCollection<ElementInit> original) {
			for (int i = 0, n = original.Count; i < n; i++) {
				this.VisitElementInitializer(original[i]);
				if (i < n - 1) {
					this.Write(",");
					this.WriteLine(Indentation.Same);
				}
			}
			return original;
		}

		protected override ReadOnlyCollection<Expression> VisitExpressionList(ReadOnlyCollection<Expression> original) {
			for (int i = 0, n = original.Count; i < n; i++) {
				this.Visit(original[i]);
				//if (i < n - 1)
				//{
				//    this.Write(",");
				//    this.WriteLine(Indentation.Same);
				//}
			}
			return original;
		}

		protected override Expression VisitInvocation(InvocationExpression iv) {
			this.Write("Invoke(");
			this.WriteLine(Indentation.Inner);
			this.VisitExpressionList(iv.Arguments);
			this.Write(", ");
			this.WriteLine(Indentation.Same);
			this.Visit(iv.Expression);
			this.WriteLine(Indentation.Same);
			this.Write(")");
			this.Indent(Indentation.Outer);
			return iv;
		}

		protected override Expression VisitLambda(LambdaExpression lambda) {
			if (lambda.Body.NodeType == ExpressionType.MemberAccess) {
				if (((MemberExpression)lambda.Body).Type == typeof(bool) && (bizRead == AiExpSqlType.aiWhere)) {
					this.Write(((MemberExpression)lambda.Body).Member.Name + " = 1");
				} else {
					this.Visit(lambda.Body);
					return lambda;
				}

			}
			this.Visit(lambda.Body);
			return lambda;
		}

		protected override Expression VisitListInit(ListInitExpression init) {
			this.Visit(init.NewExpression);
			this.Write(" {");
			this.WriteLine(Indentation.Inner);
			this.VisitElementInitializerList(init.Initializers);
			this.WriteLine(Indentation.Outer);
			this.Write("}");
			return init;
		}

		protected override Expression VisitMemberAccess(MemberExpression m) {
			m_left = m;
			this.Write("[" + m.Member.Name + "]");
			return base.VisitMemberAccess(m);
		}

		protected Expression VisitMemberAccess_Bak(MemberExpression m) {
			m_left = m;
			this.Write("[" + m.Member.Name + "]");
			// string t = m.Update.GetType().Name;
			if (m.Member.DeclaringType == typeof(string)) {
				switch (m.Member.Name) {
					case "Length":
						this.Write("LEN(");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
				}
			} else if (m.Member.DeclaringType == typeof(DateTime) || m.Member.DeclaringType == typeof(DateTimeOffset)) {
				switch (m.Member.Name) {
					case "Day":
						this.Write("DAY(");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Month":
						this.Write("MONTH(");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Year":
						this.Write("YEAR(");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Hour":
						this.Write("DATEPART(hour, ");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Minute":
						this.Write("DATEPART(minute, ");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Second":
						this.Write("DATEPART(second, ");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "Millisecond":
						this.Write("DATEPART(millisecond, ");
						this.Visit(m.Expression);
						this.Write(")");
						return m;
					case "DayOfWeek":
						this.Write("(DATEPART(weekday, ");
						this.Visit(m.Expression);
						this.Write(") - 1)");
						return m;
					case "DayOfYear":
						this.Write("(DATEPART(dayofyear, ");
						this.Visit(m.Expression);
						this.Write(") - 1)");
						return m;


				}
			}
			return base.VisitMemberAccess(m);
		}

		protected override MemberAssignment VisitMemberAssignment(MemberAssignment assignment) {
			this.Write(assignment.Member.Name);
			this.Write(" = ");
			this.Visit(assignment.Expression);
			return assignment;
		}

		protected override Expression VisitMemberInit(MemberInitExpression init) {
			this.Visit(init.NewExpression);
			this.Write(" {");
			this.WriteLine(Indentation.Inner);
			this.VisitBindingList(init.Bindings);
			this.WriteLine(Indentation.Outer);
			this.Write("}");
			return init;
		}

		protected override MemberListBinding VisitMemberListBinding(MemberListBinding binding) {
			this.Write(binding.Member.Name);
			this.Write(" = {");
			this.WriteLine(Indentation.Inner);
			this.VisitElementInitializerList(binding.Initializers);
			this.WriteLine(Indentation.Outer);
			this.Write("}");
			return binding;
		}

		protected override MemberMemberBinding VisitMemberMemberBinding(MemberMemberBinding binding) {
			this.Write(binding.Member.Name);
			this.Write(" = {");
			this.WriteLine(Indentation.Inner);
			this.VisitBindingList(binding.Bindings);
			this.WriteLine(Indentation.Outer);
			this.Write("}");
			return binding;
		}

		protected override Expression VisitMethodCall(MethodCallExpression m) {
			if (m.Method.DeclaringType == typeof(string)) {//p.Name.Contains()
				switch (m.Method.Name) {
					case "Contains":
						this.Write("(");
						this.Visit(m.Object);
						this.Write(" LIKE '%' + ");
						this.Visit(m.Arguments[0]);
						this.Write(" + '%')");
						return m;
				}
			} else if (m.Method.DeclaringType == typeof(Enumerable)) {//array.Contains(p.Name)只支持数组
				ConstantExpression constant = m.Arguments[0] as ConstantExpression;
				if (constant == null || !constant.Type.IsArray) {
					throw new NotSupportedException("不支持的写法");
				}
				Array array = (Array)constant.Value;
				if (array == null || array.Length == 0) {
					this.Write("1=0");
					return m;
				} else {
					switch (m.Method.Name) {
						case "Contains":
							this.Write("(");
							this.Visit(m.Arguments[1]);
							this.Write(" IN (");
							this.Visit(m.Arguments[0]);
							this.Write("))");
							return m;
					}
				}
			}
			throw new NotSupportedException("不支持的写法");
		}

		protected Expression VisitMethodCall_Bak(MethodCallExpression m) {
			//if (m.Object != null)
			//{
			//    this.Visit(m.Object);
			//}
			//else
			//{
			//     this.Write(this.GetTypeName(m.Method.DeclaringType));
			//}
			string bizname = m.Method.Name.ToLower();

			//if (this.GetTypeName(m.Method.DeclaringType).ToLower() != "queryable")
			//{
			//    this.Write(".");
			//}
			//    this.Write(m.Method.Name);

			switch (bizname) {
				case "where":
					if (bizRead == AiExpSqlType.aiWhere) {
						AiWhereTime = AiWhereTime + 1;
					} else {
						this.Visit(m.Arguments[0]);
						return m;
					}
					break;
				case "orderby":
				case "orderbydescending":
				case "thenbydescending":
				case "thenby":
					if (bizRead == AiExpSqlType.aiOrder) {
						AiOrderTime = AiOrderTime + 1;
					} else {
						this.Visit(m.Arguments[0]);
						return m;
					}
					break;
			}

			//bool bizisw = bizname == "where" || bizname == "orderby" || bizname == "thenby";
			//if (bizisw)
			//{
			//    this.Write(m.Method.Name);
			//}

			//this.VisitExpressionList(m.Arguments);

			if (m.Arguments.Count > 1) {

			}
			//    this.WriteLine(Indentation.Outer);
			//if (bizisw)
			//{
			//    this.Write(")");
			//}
			//return m;
			#region

			if (m.Method.DeclaringType == typeof(string)) {
				switch (m.Method.Name) {
					case "StartsWith":
						this.Write("(");
						this.Visit(m.Object);
						this.Write(" LIKE ");
						this.Visit(m.Arguments[0]);
						this.Write(" + '%')");
						return m;
					case "EndsWith":
						this.Write("(");
						this.Visit(m.Object);
						this.Write(" LIKE '%' + ");
						this.Visit(m.Arguments[0]);
						this.Write(")");
						return m;
					case "Contains":
						this.Write("(");
						this.Visit(m.Object);
						this.Write(" LIKE '%' + ");
						this.Visit(m.Arguments[0]);
						this.Write(" + '%')");
						return m;
					case "Concat":
						IList<Expression> args = m.Arguments;
						if (args.Count == 1 && args[0].NodeType == ExpressionType.NewArrayInit) {
							args = ((NewArrayExpression)args[0]).Expressions;
						}
						for (int i = 0, n = args.Count; i < n; i++) {
							if (i > 0) this.Write(" + ");
							this.Visit(args[i]);
						}
						return m;
					case "IsNullOrEmpty":
						this.Write("(");
						this.Visit(m.Arguments[0]);
						this.Write(" IS NULL OR ");
						this.Visit(m.Arguments[0]);
						this.Write(" = '')");
						return m;
					case "ToUpper":
						this.Write("UPPER(");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "ToLower":
						this.Write("LOWER(");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "Replace":
						this.Write("REPLACE(");
						this.Visit(m.Object);
						this.Write(", ");
						this.Visit(m.Arguments[0]);
						this.Write(", ");
						this.Visit(m.Arguments[1]);
						this.Write(")");
						return m;
					case "Substring":
						this.Write("SUBSTRING(");
						this.Visit(m.Object);
						this.Write(", ");
						this.Visit(m.Arguments[0]);
						this.Write(" + 1, ");
						if (m.Arguments.Count == 2) {
							this.Visit(m.Arguments[1]);
						} else {
							this.Write("8000");
						}
						this.Write(")");
						return m;
					case "Remove":
						this.Write("STUFF(");
						this.Visit(m.Object);
						this.Write(", ");
						this.Visit(m.Arguments[0]);
						this.Write(" + 1, ");
						if (m.Arguments.Count == 2) {
							this.Visit(m.Arguments[1]);
						} else {
							this.Write("8000");
						}
						this.Write(", '')");
						return m;
					case "IndexOf":
						this.Write("(CHARINDEX(");
						this.Visit(m.Arguments[0]);
						this.Write(", ");
						this.Visit(m.Object);
						if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int)) {
							this.Write(", ");
							this.Visit(m.Arguments[1]);
							this.Write(" + 1");
						}
						this.Write(") - 1)");
						return m;
					case "Trim":
						this.Write("RTRIM(LTRIM(");
						this.Visit(m.Object);
						this.Write("))");
						return m;
				}
			} else if (m.Method.DeclaringType == typeof(DateTime)) {
				switch (m.Method.Name) {
					case "op_Subtract":
						if (m.Arguments[1].Type == typeof(DateTime)) {
							this.Write("DATEDIFF(");
							this.Visit(m.Arguments[0]);
							this.Write(", ");
							this.Visit(m.Arguments[1]);
							this.Write(")");
							return m;
						}
						break;
					case "AddYears":
						this.Write("DATEADD(YYYY,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddMonths":
						this.Write("DATEADD(MM,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddDays":
						this.Write("DATEADD(DAY,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddHours":
						this.Write("DATEADD(HH,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddMinutes":
						this.Write("DATEADD(MI,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddSeconds":
						this.Write("DATEADD(SS,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
					case "AddMilliseconds":
						this.Write("DATEADD(MS,");
						this.Visit(m.Arguments[0]);
						this.Write(",");
						this.Visit(m.Object);
						this.Write(")");
						return m;
				}
			} else if (m.Method.DeclaringType == typeof(Decimal)) {
				switch (m.Method.Name) {
					case "Add":
					case "Subtract":
					case "Multiply":
					case "Divide":
					case "Remainder":
						this.Write("(");
						this.VisitValue(m.Arguments[0]);
						this.Write(" ");
						this.Write(GetOperator(m.Method.Name));
						this.Write(" ");
						this.VisitValue(m.Arguments[1]);
						this.Write(")");
						return m;
					case "Negate":
						this.Write("-");
						this.Visit(m.Arguments[0]);
						this.Write("");
						return m;
					case "Ceiling":
					case "Floor":
						this.Write(m.Method.Name.ToUpper());
						this.Write("(");
						this.Visit(m.Arguments[0]);
						this.Write(")");
						return m;
					case "Round":
						if (m.Arguments.Count == 1) {
							this.Write("ROUND(");
							this.Visit(m.Arguments[0]);
							this.Write(", 0)");
							return m;
						} else if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int)) {
							this.Write("ROUND(");
							this.Visit(m.Arguments[0]);
							this.Write(", ");
							this.Visit(m.Arguments[1]);
							this.Write(")");
							return m;
						}
						break;
					case "Truncate":
						this.Write("ROUND(");
						this.Visit(m.Arguments[0]);
						this.Write(", 0, 1)");
						return m;
				}
			} else if (m.Method.DeclaringType == typeof(Math)) {
				switch (m.Method.Name) {
					case "Abs":
					case "Acos":
					case "Asin":
					case "Atan":
					case "Cos":
					case "Exp":
					case "Log10":
					case "Sin":
					case "Tan":
					case "Sqrt":
					case "Sign":
					case "Ceiling":
					case "Floor":
						this.Write(m.Method.Name.ToUpper());
						this.Write("(");
						this.Visit(m.Arguments[0]);
						this.Write(")");
						return m;
					case "Atan2":
						this.Write("ATN2(");
						this.Visit(m.Arguments[0]);
						this.Write(", ");
						this.Visit(m.Arguments[1]);
						this.Write(")");
						return m;
					case "Log":
						if (m.Arguments.Count == 1) {
							goto case "Log10";
						}
						break;
					case "Pow":
						this.Write("POWER(");
						this.Visit(m.Arguments[0]);
						this.Write(", ");
						this.Visit(m.Arguments[1]);
						this.Write(")");
						return m;
					case "Round":
						if (m.Arguments.Count == 1) {
							this.Write("ROUND(");
							this.Visit(m.Arguments[0]);
							this.Write(", 0)");
							return m;
						} else if (m.Arguments.Count == 2 && m.Arguments[1].Type == typeof(int)) {
							this.Write("ROUND(");
							this.Visit(m.Arguments[0]);
							this.Write(", ");
							this.Visit(m.Arguments[1]);
							this.Write(")");
							return m;
						}
						break;
					case "Truncate":
						this.Write("ROUND(");
						this.Visit(m.Arguments[0]);
						this.Write(", 0, 1)");
						return m;
				}
			}
			if (m.Method.Name == "ToString") {
				if (m.Object.Type != typeof(string)) {
					this.Write("CONVERT(NVARCHAR, ");
					this.Visit(m.Object);
					this.Write(")");
				} else {
					this.Visit(m.Object);
				}
				return m;
			} else if (!m.Method.IsStatic && m.Method.Name == "CompareTo" && m.Method.ReturnType == typeof(int) && m.Arguments.Count == 1) {
				this.Write("(CASE WHEN ");
				this.Visit(m.Object);
				this.Write(" = ");
				this.Visit(m.Arguments[0]);
				this.Write(" THEN 0 WHEN ");
				this.Visit(m.Object);
				this.Write(" < ");
				this.Visit(m.Arguments[0]);
				this.Write(" THEN -1 ELSE 1 END)");
				return m;
			} else if (m.Method.IsStatic && m.Method.Name == "Compare" && m.Method.ReturnType == typeof(int) && m.Arguments.Count == 2) {
				this.Write("(CASE WHEN ");
				this.Visit(m.Arguments[0]);
				this.Write(" = ");
				this.Visit(m.Arguments[1]);
				this.Write(" THEN 0 WHEN ");
				this.Visit(m.Arguments[0]);
				this.Write(" < ");
				this.Visit(m.Arguments[1]);
				this.Write(" THEN -1 ELSE 1 END)");
				return m;
			}
			#endregion

			if (m.Arguments.Count > 1) {
				this.WriteLine(Indentation.Outer);
			}
			base.VisitMethodCall(m);
			if (m.Arguments.Count > 1) {
				switch (bizname) {
					case "orderbydescending":
					case "thenbydescending": aiOrdeRsult = aiOrdeRsult + " Desc"; break;

				}
				if (bizRead == AiExpSqlType.aiOrder) {
					aiOrdeRsult = aiOrdeRsult + ",";
				} else {

					aiWhereResult = aiWhereResult + " and ";


				}
				this.WriteLine(Indentation.Outer);
			}

			return m;
		}

		protected Expression VisitValue(Expression expr) {
			if (IsPredicate(expr)) {
				this.Write("CASE WHEN (");
				this.Visit(expr);
				this.Write(") THEN 1 ELSE 0 END");
				return expr;
			}
			return expr;
		}
		protected override NewExpression VisitNew(NewExpression nex) {
			//this.Write("new ");
			//this.Write(this.GetTypeName(nex.Constructor.DeclaringType));
			//this.Write("(");
			//if (nex.Arguments.Count > 1)
			//    this.WriteLine(Indentation.Inner);
			//this.VisitExpressionList(nex.Arguments);
			//if (nex.Arguments.Count > 1)
			//    this.WriteLine(Indentation.Outer);
			//this.Write(")");
			//return nex;
			if (nex.Constructor.DeclaringType == typeof(DateTime)) {
				if (nex.Arguments.Count == 3) {
					this.Write("Convert(DateTime, ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[0]);
					this.Write(") + '/' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[1]);
					this.Write(") + '/' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[2]);
					this.Write("))");
					return nex;
				} else if (nex.Arguments.Count == 6) {
					this.Write("Convert(DateTime, ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[0]);
					this.Write(") + '/' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[1]);
					this.Write(") + '/' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[2]);
					this.Write(") + ' ' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[3]);
					this.Write(") + ':' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[4]);
					this.Write(") + ':' + ");
					this.Write("Convert(nvarchar, ");
					this.Visit(nex.Arguments[5]);
					this.Write("))");
					return nex;
				}
			}
			return base.VisitNew(nex);
		}

		protected override Expression VisitNewArray(NewArrayExpression na) {
			this.Write("new ");
			this.Write(this.GetTypeName(AiTypeHelper.GetElementType(na.Type)));
			this.Write("[] {");
			if (na.Expressions.Count > 1)
				this.WriteLine(Indentation.Inner);
			this.VisitExpressionList(na.Expressions);
			if (na.Expressions.Count > 1)
				this.WriteLine(Indentation.Outer);
			this.Write("}");
			return na;
		}

		protected override Expression VisitParameter(ParameterExpression p) {
			// this.Write(p.Name);
			return p;
		}

		protected override Expression VisitTypeIs(TypeBinaryExpression b) {
			this.Visit(b.Expression);
			this.Write(" is ");
			this.Write(this.GetTypeName(b.TypeOperand));
			return b;
		}

		protected override Expression VisitUnknown(Expression expression) {
			this.Write(expression.ToString());
			return expression;
		}

		protected virtual bool IsBoolean(Type type) {
			return type == typeof(bool) || type == typeof(bool?);
		}

		protected virtual bool IsPredicate(Expression expr) {
			switch (expr.NodeType) {
				case ExpressionType.And:
				case ExpressionType.AndAlso:
				case ExpressionType.Or:
				case ExpressionType.OrElse:
					return IsBoolean(((BinaryExpression)expr).Type);
				case ExpressionType.Not:
					return IsBoolean(((UnaryExpression)expr).Type);
				case ExpressionType.Equal:
				case ExpressionType.NotEqual:
				case ExpressionType.LessThan:
				case ExpressionType.LessThanOrEqual:
				case ExpressionType.GreaterThan:
				case ExpressionType.GreaterThanOrEqual:
				case (ExpressionType)DbExpressionType.IsNull:
				case (ExpressionType)DbExpressionType.Between:
				case (ExpressionType)DbExpressionType.Exists:
				case (ExpressionType)DbExpressionType.In:
					return true;
				case ExpressionType.Call:
					return IsBoolean(((MethodCallExpression)expr).Type);
				default:
					return false;
			}
		}

		internal enum DbExpressionType {
			Table = 1000, // make sure these don't overlap with ExpressionType
			ClientJoin,
			Column,
			Select,
			Projection,
			Entity,
			Join,
			Aggregate,
			Scalar,
			Exists,
			In,
			Grouping,
			AggregateSubquery,
			IsNull,
			Between,
			RowCount,
			NamedValue,
			OuterJoined,
			Insert,
			Update,
			Delete,
			Batch,
			Function,
			Block,
			If,
			Declaration,
			Variable
		}

		protected virtual string GetOperator(string methodName) {
			switch (methodName) {
				case "Add": return "+";
				case "Subtract": return "-";
				case "Multiply": return "*";
				case "Divide": return "/";
				case "Negate": return "-";
				case "Remainder": return "%";
				default: return null;
			}
		}

		protected virtual string GetOperator(UnaryExpression u) {
			switch (u.NodeType) {
				case ExpressionType.Negate:
				case ExpressionType.NegateChecked:
					return "-";
				case ExpressionType.UnaryPlus:
					return "+";
				case ExpressionType.Not:
					return IsBoolean(u.Operand.Type) ? "NOT" : "~";
				default:
					return "";
			}
		}

		protected virtual string GetOperator(BinaryExpression b) {
			switch (b.NodeType) {
				case ExpressionType.And:
				case ExpressionType.AndAlso:
					return (IsBoolean(b.Left.Type)) ? "AND" : "&";
				case ExpressionType.Or:
				case ExpressionType.OrElse:
					return (IsBoolean(b.Left.Type) ? "OR" : "|");
				case ExpressionType.Equal:
					return "=";
				case ExpressionType.NotEqual:
					return "<>";
				case ExpressionType.LessThan:
					return "<";
				case ExpressionType.LessThanOrEqual:
					return "<=";
				case ExpressionType.GreaterThan:
					return ">";
				case ExpressionType.GreaterThanOrEqual:
					return ">=";
				case ExpressionType.Add:
				case ExpressionType.AddChecked:
					return "+";
				case ExpressionType.Subtract:
				case ExpressionType.SubtractChecked:
					return "-";
				case ExpressionType.Multiply:
				case ExpressionType.MultiplyChecked:
					return "*";
				case ExpressionType.Divide:
					return "/";
				case ExpressionType.Modulo:
					return "%";
				case ExpressionType.ExclusiveOr:
					return "^";
				case ExpressionType.LeftShift:
					return "<<";
				case ExpressionType.RightShift:
					return ">>";
				default:
					return "";
			}
		}
	}
}