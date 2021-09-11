#nullable enable

using System;
using System.IO;

namespace DeBugFinderPatcher {
	public class ArrayCursor<T> where T : class {
		private readonly T[] array;
		private int position = 0;

		public ArrayCursor(T[] array) {
			this.array = array;
		}

		public T? GetCurrent() {
			return this.AtEnd() ? null : this.array[this.position];
		}

		public void MoveNext() {
			this.position++;
			if(this.position > this.array.Length) 
				this.position = this.array.Length;
		}

		public bool AtEnd() {
			return this.position == this.array.Length;
		}

		public void Seek(SeekOrigin whence, int offset) {
			int origin = whence switch {
				SeekOrigin.Begin => 0,
				SeekOrigin.Current => this.position,
				SeekOrigin.End => this.array.Length,
				_ => throw new ArgumentException("Invalid origin", nameof(whence))
			};

			int destination = origin + offset;
			if(destination < 0)
				destination = 0;
			if(destination > this.array.Length)
				destination = this.array.Length;
			this.position = destination;
		}

		public int Tell() {
			return this.position;
		}
	}
}
