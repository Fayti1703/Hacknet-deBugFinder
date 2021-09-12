#!/bin/bash

# Load configuration
source run.sh.config

# sh boolean, be careful
unknown_op=1

move_to_directory() {
	if ! cd "$1"; then
		printf "Failed to move to $2 directory. Check that it exists.\n" >&2;
		return 1;
	fi
	return 0;
}

do_operation() {
	unknown_op=1
	case "$1" in
		"init")
			cp "$hacknet_dir"/{FNA,Steamworks.NET,AlienFXManagedWrapper3.5}.dll .;
			;;
		"workaround-exe")
			cp "$hacknet_dir"/Hacknet.exe .;
			;;
		"patcher")
			"$builder" ../DeBugFinderPatcher/DeBugFinderPatcher.csproj /p:Configuration="$configuration";
			;;
		"spit")
			"$exe_prefix" ./DeBugFinderPatcher.exe -exeDir "$hacknet_dir" -spit;
			;;
		"build")
			"$builder" ../DeBugFinder/DeBugFinder.csproj /p:Configuration="$configuration";
			;;
		"patch")
			"$exe_prefix" ./DeBugFinderPatcher.exe -exeDir "$hacknet_dir" -nolaunch;
			;;
		"copy")
			cp -v DeBugFinder.dll "$hacknet_dir";
			;;
		*)
			printf "Unknown operation: %s\n" "$1" >&2
			unknown_op=0
			return 0;
	esac
}

ourdir="$(readlink -f "$(dirname "$0")")"
if ! move_to_directory "$ourdir/lib" "'lib'"; then
	exit 2;
fi
while [[ "$#" -gt 0 ]]; do
	if ! do_operation "$1"; then
		if [[ unknown_op -ne 0 ]]; then
			printf "Failure in '%s' operation.\n" "$1" >&2
		fi
		exit 1
	fi
	shift;
done
