using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Mono.Cecil;
using static DeBugFinderPatcher.AccessLevel;

namespace DeBugFinderPatcher {
	abstract public class GenericTypeTaskItem {
		protected GenericTypeTaskItem(string typeName, AccessLevel? targetLevel = null) {
			this.typeName = typeName;
			this.targetLevel = targetLevel;
		}

		public string typeName { get; }
		protected AccessLevel? targetLevel;

		public void addFieldModification(string name, AccessLevel newLevel) {
			this.fieldModifications[name] = newLevel;
		}

		public void addMethodModification(string name, AccessLevel newLevel) {
			this.methodModifications[name] = newLevel;
		}

		public void addTypeModification(GenericTypeTaskItem modification) {
			this.typeModifications.Add(modification.typeName, modification);
		}

		public AccessLevel? newDefaultFieldAccess = null;
		public AccessLevel? newDefaultMethodAccess = null;
		public AccessLevel? newDefaultTypeAccess = null;

		private readonly Dictionary<string, AccessLevel> fieldModifications = new Dictionary<string, AccessLevel>();
		private readonly Dictionary<string, AccessLevel> methodModifications = new Dictionary<string, AccessLevel>();

		private readonly Dictionary<string, GenericTypeTaskItem> typeModifications =
			new Dictionary<string, GenericTypeTaskItem>();


		protected void execute(TypeDefinition target) {
			if(this.targetLevel.HasValue)
				this.applyTypeAccess(target);

			if(this.newDefaultFieldAccess.HasValue) {
				foreach(FieldDefinition field in target.Fields) {
					if(!this.fieldModifications.TryGetValue(field.Name, out AccessLevel newLevel))
						newLevel = this.newDefaultFieldAccess.Value;
					field.MakeFieldAccess(newLevel);
				}
			} else {
				foreach((string fieldName, AccessLevel newLevel) in this.fieldModifications) {
					target.GetField(fieldName).MakeFieldAccess(newLevel);
				}
			}

			if(this.newDefaultMethodAccess.HasValue) {
				foreach(MethodDefinition method in target.Methods) {
					if(!this.methodModifications.TryGetValue(method.Name, out AccessLevel newLevel))
						newLevel = this.newDefaultMethodAccess.Value;
					method.MakeMethodAccess(newLevel);
				}
			} else {
				foreach((string methodName, AccessLevel newLevel) in this.methodModifications) {
					target.GetMethod(methodName).MakeMethodAccess(newLevel);
				}
			}

			if(!this.newDefaultTypeAccess.HasValue && this.typeModifications.Count <= 0) return;
			foreach(TypeDefinition type in target.NestedTypes) {
				if(this.typeModifications.TryGetValue(type.Name, out GenericTypeTaskItem nestedTask)) {
					if(!nestedTask.targetLevel.HasValue && this.newDefaultTypeAccess.HasValue)
						type.MakeNestedAccess(this.newDefaultTypeAccess.Value);
					nestedTask.execute(type);
				} else if(this.newDefaultTypeAccess.HasValue)
					type.MakeNestedAccess(this.newDefaultTypeAccess.Value);
			}
		}

		abstract protected void applyTypeAccess(TypeDefinition target);

		virtual protected string getToStringPrefix() {
			return "Task: At Type ";
		}

		override public string ToString() {
			bool Sub1<T>(StringBuilder dst, T? nullable, string prefix) where T : struct {
				if(!nullable.HasValue) return false;
				dst.Append(prefix);
				dst.Append(nullable.Value);
				return true;
			}

			StringBuilder taskStr = new StringBuilder(this.getToStringPrefix())
				.Append(this.typeName);
			bool written = false;
			written |= Sub1(taskStr, this.newDefaultFieldAccess, ",\n\tset all fields to ");
			written |= Sub1(taskStr, this.newDefaultMethodAccess, ",\n\tset all methods to ");
			written |= Sub1(taskStr, this.newDefaultTypeAccess, ",\n\tset all nested types to ");
			if(this.fieldModifications.Count + this.methodModifications.Count > 0) {
				taskStr.AppendLine(written ? "\nand set" : ", set");
				written = true;
				foreach((string fieldName, AccessLevel newLevel) in this.fieldModifications)
					taskStr.Append("\tthe field `").Append(fieldName).Append("` to ").AppendLine(newLevel.ToString());

				foreach((string methodName, AccessLevel newLevel) in this.methodModifications)
					taskStr.Append("\tthe method `").Append(methodName).Append("` to ").AppendLine(newLevel.ToString());
			}

			if(this.typeModifications.Count > 0) {
				if(written)
					taskStr.Append(".\nAdditionally, ");
				taskStr.Append("modify nested types as follows: ");
				foreach((string _, GenericTypeTaskItem nestedTask) in this.typeModifications)
					taskStr.Append(nestedTask);
			}

			return taskStr.ToString();
		}
	}

	public class TypeTaskItem : GenericTypeTaskItem {
		public TypeTaskItem(string typeName, AccessLevel? targetLevel = null) : base(typeName, targetLevel) {
			if(!targetLevel.HasValue) return;
			if(targetLevel.Value != Public && targetLevel.Value != Internal) {
				throw new ArgumentOutOfRangeException(nameof(targetLevel), targetLevel,
					"For a non-nested type, the only valid access levels are \"public\" and \"internal\"."
				);
			}
		}

		override protected void applyTypeAccess(TypeDefinition target) {
			Debug.Assert(this.targetLevel.HasValue);
			switch(this.targetLevel.Value) {
				case Public:
					target.IsPublic = true;
					break;
				case Internal:
					target.IsNotPublic = true;
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public void execute(ModuleDefinition target) {
			TypeDefinition targetDef = target.GetType(this.typeName);
			if(targetDef == null)
				throw new Exception($"Could not execute task: No such type: {this.typeName}");
			this.execute(targetDef);
		}
	}


	public class NestedTypeTaskItem : GenericTypeTaskItem {
		public NestedTypeTaskItem(string typeName, AccessLevel? targetLevel = null) : base(typeName, targetLevel) { }

		override protected void applyTypeAccess(TypeDefinition target) {
			Debug.Assert(this.targetLevel.HasValue);
			target.MakeNestedAccess(this.targetLevel.Value);
		}

		override protected string getToStringPrefix() {
			return "For the nested type ";
		}
	}
}
