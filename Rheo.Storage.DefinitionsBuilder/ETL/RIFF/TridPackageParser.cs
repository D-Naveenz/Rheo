using Rheo.Storage.Analysing.Models;

namespace Rheo.Storage.DefinitionsBuilder.ETL.RIFF
{
    public static class TridPackageParser
    {
        private const uint RIFF_CHUNK_ID = 0x46464952; // "RIFF"
        private const uint TRID_CHUNK_ID = 0x44495254; // "TRID"
        private const uint DEF_CHUNK_ID = 0x20464544;   // "DEF "
        private const uint DATA_CHUNK_ID = 0x41544144;  // "DATA"
        private const uint PATT_CHUNK_ID = 0x54544150;  // "PATT"
        private const uint STRN_CHUNK_ID = 0x4E525453;  // "STRN"
        private const uint INFO_CHUNK_ID = 0x4F464E49;  // "INFO"
        private const uint TYPE_CHUNK_ID = 0x45505954;  // "TYPE"
        private const uint EXT_CHUNK_ID = 0x20545845;   // "EXT "
        private const uint TAG_CHUNK_ID = 0x20474154;   // "TAG "
        private const uint MIME_CHUNK_ID = 0x454D494D;  // "MIME"
        private const uint NAME_CHUNK_ID = 0x454D414E;  // "NAME"
        private const uint FNUM_CHUNK_ID = 0x4D554E46;  // "FNUM"
        private const uint RURL_CHUNK_ID = 0x4C525552;  // "RURL"
        private const uint USER_CHUNK_ID = 0x52455355;  // "USER"
        private const uint MAIL_CHUNK_ID = 0x4C49414D;  // "MAIL"
        private const uint HOME_CHUNK_ID = 0x454D4F48;  // "HOME"
        private const uint REM_CHUNK_ID = 0x204D4552;    // "REM "

        /// <summary>
        /// Parses a TrID definitions package from the specified file path and returns a block of definitions.
        /// </summary>
        /// <remarks>This method reads the TrID definitions package, validates its structure, and extracts
        /// the definitions. The definitions are grouped by the first byte of their patterns for efficient lookup.
        /// Definitions without a valid first byte are grouped under a catch-all key.</remarks>
        /// <param name="filePath">The path to the file containing the TrID definitions package. The file must be a valid RIFF file with a TrID
        /// definitions block.</param>
        /// <returns>A Dictionary containing the parsed definitions, organized by the first byte of their patterns.</returns>
        /// <exception cref="InvalidDataException">Thrown if the file is not a valid RIFF file or does not contain a TrID definitions block.</exception>
        public static Dictionary<int, List<TrIDDefinition>> ParsePackage(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            using var reader = new BinaryReader(fileStream);

            // Read RIFF header
            uint riffId = reader.ReadUInt32();
            if (riffId != RIFF_CHUNK_ID)
                throw new InvalidDataException("Not a valid RIFF file");

            uint fileSize = reader.ReadUInt32();

            uint tridId = reader.ReadUInt32();
            if (tridId != TRID_CHUNK_ID)
                throw new InvalidDataException("Not a TrID definitions package");

            // Read info block (12 bytes)
            byte[] infoBlock = reader.ReadBytes(12);
            int definitionCount = BitConverter.ToInt32(infoBlock, 8);

            // Read length of definitions block
            uint blockLength = reader.ReadUInt32();

            // Read definitions block
            byte[] definitionsBlock = reader.ReadBytes((int)blockLength);

            var definitions = ParseDefinitionsBlock(definitionsBlock);

            // Organize definitions by first byte
            var definitionsByFirstByte = new Dictionary<int, List<TrIDDefinition>>();

            // Initialize for -1 (catch-all) and 0-255
            for (int b = -1; b < 256; b++)
            {
                definitionsByFirstByte[b] = [];
            }

            foreach (var definition in definitions)
            {
                if (definition.Patterns.Count > 0)
                {
                    var firstPattern = definition.Patterns[0];
                    if (firstPattern.Position == 0 && firstPattern.Data.Length > 0)
                    {
                        definitionsByFirstByte[firstPattern.Data[0]].Add(definition);
                    }
                    else
                    {
                        definitionsByFirstByte[-1].Add(definition);
                    }
                }
                else
                {
                    definitionsByFirstByte[-1].Add(definition);
                }
            }

            return definitionsByFirstByte;
        }

        private static List<TrIDDefinition> ParseDefinitionsBlock(byte[] block)
        {
            var definitions = new List<TrIDDefinition>();
            int position = 0;

            while (position < block.Length - 8)
            {
                uint chunkId = BitConverter.ToUInt32(block, position);
                if (chunkId != DEF_CHUNK_ID)
                {
                    position += 8;
                    continue;
                }

                uint chunkLength = BitConverter.ToUInt32(block, position + 4);
                byte[] definitionBlock = new byte[chunkLength];
                Array.Copy(block, position + 8, definitionBlock, 0, chunkLength);

                var definition = ParseDefinitionBlock(definitionBlock);
                definitions.Add(definition);

                position += 8 + (int)chunkLength;
            }

            return definitions;
        }

        private static TrIDDefinition ParseDefinitionBlock(byte[] block)
        {
            var definition = new TrIDDefinition();
            int position = 0;

            while (position < block.Length - 8)
            {
                uint chunkId = BitConverter.ToUInt32(block, position);
                uint chunkLength = BitConverter.ToUInt32(block, position + 4);
                byte[] chunk = new byte[chunkLength];
                Array.Copy(block, position + 8, chunk, 0, chunkLength);

                switch (chunkId)
                {
                    case DATA_CHUNK_ID:
                        ParseDataChunk(chunk, definition);
                        break;
                    case INFO_CHUNK_ID:
                        ParseInfoChunk(chunk, definition);
                        break;
                }

                position += 8 + (int)chunkLength;
            }

            return definition;
        }

        private static void ParseDataChunk(byte[] chunk, TrIDDefinition definition)
        {
            int position = 0;

            while (position < chunk.Length - 8)
            {
                uint subChunkId = BitConverter.ToUInt32(chunk, position);
                uint subChunkLength = BitConverter.ToUInt32(chunk, position + 4);
                byte[] subChunk = new byte[subChunkLength];
                Array.Copy(chunk, position + 8, subChunk, 0, subChunkLength);

                switch (subChunkId)
                {
                    case PATT_CHUNK_ID:
                        definition.Patterns = ParsePatternsChunk(subChunk);
                        break;
                    case STRN_CHUNK_ID:
                        definition.Strings = ParseStringsChunk(subChunk);
                        break;
                }

                position += 8 + (int)subChunkLength;
            }
        }

        private static List<Pattern> ParsePatternsChunk(byte[] chunk)
        {
            var patterns = new List<Pattern>();
            ushort patternCount = BitConverter.ToUInt16(chunk, 0);
            int position = 2;

            for (int i = 0; i < patternCount; i++)
            {
                ushort patternPosition = BitConverter.ToUInt16(chunk, position);
                ushort patternLength = BitConverter.ToUInt16(chunk, position + 2);
                byte[] patternData = new byte[patternLength];
                Array.Copy(chunk, position + 4, patternData, 0, patternLength);

                patterns.Add(new Pattern
                {
                    Position = patternPosition,
                    Data = patternData
                });

                position += 4 + patternLength;
            }

            return patterns;
        }

        private static List<byte[]> ParseStringsChunk(byte[] chunk)
        {
            var strings = new List<byte[]>();
            ushort stringCount = BitConverter.ToUInt16(chunk, 0);
            int position = 2;

            for (int i = 0; i < stringCount; i++)
            {
                uint stringLength = BitConverter.ToUInt32(chunk, position);
                byte[] stringData = new byte[stringLength];
                Array.Copy(chunk, position + 4, stringData, 0, stringLength);

                strings.Add(stringData);
                position += 4 + (int)stringLength;
            }

            return strings;
        }

        private static void ParseInfoChunk(byte[] chunk, TrIDDefinition definition)
        {
            int position = 0;

            while (position < chunk.Length - 6)
            {
                uint infoType = BitConverter.ToUInt32(chunk, position);
                ushort infoLength = BitConverter.ToUInt16(chunk, position + 4);
                byte[] infoData = new byte[infoLength];
                Array.Copy(chunk, position + 6, infoData, 0, infoLength);

                string text = System.Text.Encoding.UTF8.GetString(infoData);

                switch (infoType)
                {
                    case TYPE_CHUNK_ID:
                        definition.FileType = text;
                        break;
                    case EXT_CHUNK_ID:
                        definition.Extension = text.ToLowerInvariant();
                        break;
                    case TAG_CHUNK_ID:
                        definition.Tag = BitConverter.ToInt32(infoData, 0);
                        break;
                    case MIME_CHUNK_ID:
                        definition.MimeType = text;
                        break;
                    case NAME_CHUNK_ID:
                        definition.FileName = text;
                        break;
                    case FNUM_CHUNK_ID:
                        definition.FileCount = BitConverter.ToInt32(infoData, 0);
                        break;
                    case RURL_CHUNK_ID:
                        definition.ReferenceUrl = text;
                        break;
                    case USER_CHUNK_ID:
                        definition.User = text;
                        break;
                    case MAIL_CHUNK_ID:
                        definition.Email = text;
                        break;
                    case HOME_CHUNK_ID:
                        definition.Home = text;
                        break;
                    case REM_CHUNK_ID:
                        definition.Remarks = text;
                        break;
                }

                position += 6 + infoLength;
            }
        }
    }
}
