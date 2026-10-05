using System;

namespace FFLogsUploaderPlugin;

// ReSharper disable once InconsistentNaming
public class FFLogsException(string message) : Exception(message);
public class SplitLogException(string message) : FFLogsException(message);
