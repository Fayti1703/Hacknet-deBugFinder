#nullable enable
using System;
using Hacknet;
using Microsoft.Xna.Framework;

namespace Pathfinder {
	public static class NearbyNodeOffsetViewer {

		private static bool active;
		
		private static Computer? rootNode;
		private static Computer? leafNode;

		private static double frameDelay = 16.66;
		private static double delayCounter = 0;
		private static int total;
		private static int currentPosition;
		private static int minimumPosition = 0;
		private static int maximumPosition;
		private static float extraDistance;

		private const string allOps = "'root-node' / 'leaf-node' / 'start' / 'stop' / 'config' / 'once'";


		public static void HacknetInterface(string[] args) {
			if (args.Length < 1) {
				OS.currentInstance.write("No operation provided.");
				OS.currentInstance.write($"Add one of {allOps}.");
				return;
			}
			switch (args[0]) {
				case "root-node":
					rootNode = OS.currentInstance.connectedComp;
					break;
				case "leaf-node":
					leafNode = OS.currentInstance.connectedComp;
					break;
				case "start":
					if (rootNode == null)
						OS.currentInstance.write("No root node selected.");
					else if(leafNode == null)
						OS.currentInstance.write("No leaf node selected.");
					else if(active)
						OS.currentInstance.write("Already active.");
					else {
						active = true;
						OS.currentInstance.write("Begun spinning the node.");
					}
					break;
				case "stop":
					if (active) {
						active = false;
						delayCounter = 0;
						OS.currentInstance.write("Stopped spinning the node.");
					} else
						OS.currentInstance.write("Not active.");
					break;
				case "config": {
					if (args.Length < 3) {
						OS.currentInstance.write("Too few arguments.");
						OS.currentInstance.write("nodepos config <framedelay> <total> [extradist] [max-pos] [min-pos]");
						return;
					}

					double t_frameDelay;
					int t_total;
					float t_extraDistance;
					int t_maximumPosition;
					int t_minimumPosition;
					try {
						t_frameDelay = Convert.ToDouble(args[1]);
						t_total = Convert.ToInt32(args[2]);
						t_extraDistance = args.Length >= 4 ? Convert.ToSingle(args[3]) : 0.0f;
						t_maximumPosition = args.Length >= 5 ? Convert.ToInt32(args[4]) : t_total;
						t_minimumPosition = args.Length >= 6 ? Convert.ToInt32(args[5]) : 0;
					} catch (FormatException) {
						OS.currentInstance.write("Argument format error.");
						OS.currentInstance.write("nodepos config <framedelay>(double) <total>(int32) [extradist](single) [max-pos](int32)");
						return;
					}

					if (t_minimumPosition > t_maximumPosition) {
						OS.currentInstance.write("Invalid config: min > max");
						return;
					}

					frameDelay = t_frameDelay;
					total = t_total;
					extraDistance = t_extraDistance;
					maximumPosition = t_maximumPosition;
					minimumPosition = t_minimumPosition;

					break;
				}
				case "once": {
					if (args.Length < 3) {
						OS.currentInstance.write("Too few arguments.");
						OS.currentInstance.write("nodepos once <pos> <total> [extradist]");
					}

					if (rootNode == null) {
						OS.currentInstance.write("No root node selected.");
						return;
					}
					if (leafNode == null) {
						OS.currentInstance.write("No leaf node selected.");
						return;
					}

					int position;
					int total;
					float extraDistance;
					try {
						position = Convert.ToInt32(args[1]);
						total = Convert.ToInt32(args[2]);
						extraDistance = args.Length >= 3 ? Convert.ToSingle(args[3]) : 0.0f;
					} catch (FormatException) {
						OS.currentInstance.write("Argument format error.");
						OS.currentInstance.write("nodepos config <pos>(int32) <total>(int32) [extradist](single)");
						return;
					}

					repositionNode(position, total, extraDistance);
					break;
				}
				default:
					OS.currentInstance.write("No such operation.");
					OS.currentInstance.write($"Use one of {allOps}.");
					break;
			}
		}

		private static void repositionNode(int position, int total, float extraDistance) {
			if(rootNode == null)
				throw new InvalidOperationException("No root node");
			if(leafNode == null)
				throw new InvalidOperationException("No leaf node");
			leafNode.location = rootNode.location +
				Corporation.getNearbyNodeOffset(
					rootNode.location,
					position,
					total,
					OS.currentInstance.netMap,
					extraDistance,
					true
				);
		}

		public static void onUpdate(GameTime deltaT) {
			if (!active) return;
			delayCounter += deltaT.ElapsedGameTime.TotalMilliseconds;
			if (delayCounter < frameDelay) return;
			long loopArounds = (long) (delayCounter / frameDelay);
			loopArounds %= (maximumPosition - minimumPosition + 1);
			currentPosition = (int)loopArounds + minimumPosition;
			delayCounter %= frameDelay;
		}

		public static void onSessionStop() {
			active = false;
			delayCounter = 0;
			rootNode = null;
			leafNode = null;
		}
	}
}
