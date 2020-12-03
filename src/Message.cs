using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace HL7.Dotnetcore
{
    public class Message
    {
        private List<string> _allSegments = null;
        internal Dictionary<string, List<Segment>> SegmentList { get; set; } = new Dictionary<string, List<Segment>>();

        public string HL7Message { get; set; }
        public string Version { get; set; }
        public string MessageStructure { get; set; }
        public string MessageControlID { get; set; }
        public string ProcessingID { get; set; }
        public short SegmentCount { get; set; }
        public HL7Encoding Encoding { get; set; } = new HL7Encoding();

        private const string SegmentRegex = "^[A-Z][A-Z][A-Z1-9]$";
        private const string FieldRegex = @"^([0-9]+)([\(\[]([0-9]+)[\)\]]){0,1}$";
        private const string OtherRegEx = @"^[1-9]([0-9]{1,2})?$";

        public Message()
        { }

        public Message(string strMessage)
        {
            HL7Message = strMessage;
        }

        public override bool Equals(object obj)
        {
            if (obj is Message)
                return Equals((obj as Message).HL7Message);

            if (obj is string)
            {
                var arr1 = MessageHelper.SplitString(HL7Message, Encoding.SegmentDelimiter, StringSplitOptions.RemoveEmptyEntries);
                var arr2 = MessageHelper.SplitString(obj as string, Encoding.SegmentDelimiter, StringSplitOptions.RemoveEmptyEntries);

                return arr1.SequenceEqual(arr2);
            }

            return false;
        }

        public override int GetHashCode()
        {
            return HL7Message.GetHashCode();
        }

        /// <summary>
        /// Parse the HL7 message in text format, throws HL7Exception if error occurs
        /// </summary>
        /// <returns>boolean</returns>
        public bool ParseMessage()
        {
            var isValid = false;
            var isParsed = false;

            try
            {
                isValid = ValidateMessage();
            }
            catch (HL7Exception)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Unhandled Exception in validation - " + ex.Message, HL7Exception.BAD_MESSAGE);
            }

            if (isValid)
            {
                try
                {
                    if (_allSegments == null || _allSegments.Count <= 0)
                        _allSegments = MessageHelper.SplitMessage(HL7Message);

                    short segSeqNo = 0;

                    foreach (var strSegment in _allSegments)
                    {
                        if (string.IsNullOrWhiteSpace(strSegment))
                            continue;

                        var newSegment = new Segment(Encoding)
                        {
                            Name = strSegment.Substring(0, 3),
                            Value = strSegment,
                            SequenceNo = segSeqNo++
                        };

                        AddNewSegment(newSegment);
                    }

                    SegmentCount = segSeqNo;

                    var strSerializedMessage = string.Empty;

                    try
                    {
                        strSerializedMessage = SerializeMessage(false);
                    }
                    catch (HL7Exception ex)
                    {
                        throw new HL7Exception("Failed to serialize parsed message with error - " + ex.Message, HL7Exception.PARSING_ERROR);
                    }

                    if (!string.IsNullOrEmpty(strSerializedMessage))
                    {
                        if (Equals(strSerializedMessage))
                            isParsed = true;
                    }
                    else
                    {
                        throw new HL7Exception("Unable to serialize to original message - ", HL7Exception.PARSING_ERROR);
                    }
                }
                catch (Exception ex)
                {
                    throw new HL7Exception("Failed to parse the message with error - " + ex.Message, HL7Exception.PARSING_ERROR);
                }
            }

            return isParsed;
        }

        /// <summary>
        /// Serialize the message in text format
        /// </summary>
        /// <param name="validate">Validate the message before serializing</param>
        /// <returns>string with HL7 message</returns>
        public string SerializeMessage(bool validate)
        {
            if (validate && !ValidateMessage())
                throw new HL7Exception("Failed to validate the updated message", HL7Exception.BAD_MESSAGE);

            var strMessage = new StringBuilder();
            var currentSegName = string.Empty;
            var segListOrdered = GetAllSegmentsInOrder();

            try
            {
                try
                {
                    foreach (var seg in segListOrdered)
                    {
                        currentSegName = seg.Name;

                        strMessage.Append(seg.Name);

                        if (seg.FieldList.Count > 0)
                            strMessage.Append(Encoding.FieldDelimiter);

                        var startField = currentSegName == "MSH" ? 1 : 0;

                        for (var i = startField; i < seg.FieldList.Count; i++)
                        {
                            if (i > startField)
                                strMessage.Append(Encoding.FieldDelimiter);

                            var field = seg.FieldList[i];

                            if (field.IsDelimiters)
                            {
                                strMessage.Append(field.Value);
                                continue;
                            }

                            if (field.HasRepetitions)
                            {
                                for (var j = 0; j < field.RepeatitionList.Count; j++)
                                {
                                    if (j > 0)
                                        strMessage.Append(Encoding.RepeatDelimiter);

                                    SerializeField(field.RepeatitionList[j], strMessage);
                                }
                            }
                            else
                                SerializeField(field, strMessage);
                        }

                        strMessage.Append(Encoding.SegmentDelimiter);
                    }
                }
                catch (Exception ex)
                {
                    if (currentSegName == "MSH")
                        throw new HL7Exception("Failed to serialize the MSH segment with error - " + ex.Message, HL7Exception.SERIALIZATION_ERROR);
                    else
                        throw;
                }

                return strMessage.ToString();
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Failed to serialize the message with error - " + ex.Message, HL7Exception.SERIALIZATION_ERROR);
            }
        }

        /// <summary>
        /// Get the Value of specific Field/Component/SubCpomponent, throws error if field/component index is not valid
        /// </summary>
        /// <param name="strValueFormat">Field/Component position in format SEGMENTNAME.FieldIndex.ComponentIndex.SubComponentIndex example PID.5.2</param>
        /// <returns>Value of specified field/component/subcomponent</returns>
        public string GetValue(string strValueFormat)
        {
            var allComponents = MessageHelper.SplitString(strValueFormat, new char[] { '.' });
            var comCount = allComponents.Count;
            var isValid = ValidateValueFormat(allComponents);

            string strValue;
            if (isValid)
            {
                var segmentName = allComponents[0];

                if (SegmentList.ContainsKey(segmentName))
                {
                    var segment = SegmentList[segmentName].First();

                    int componentIndex;
                    if (comCount == 4)
                    {
                        _ = int.TryParse(allComponents[2], out componentIndex);
                        _ = int.TryParse(allComponents[3], out var subComponentIndex);

                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            strValue = field.ComponentList[componentIndex - 1].SubComponentList[subComponentIndex - 1].Value;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("SubComponent not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else if (comCount == 3)
                    {
                        _ = int.TryParse(allComponents[2], out componentIndex);

                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            strValue = field.ComponentList[componentIndex - 1].Value;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("Component not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else if (comCount == 2)
                    {
                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            strValue = field.Value;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("Field not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else
                    {
                        try
                        {
                            strValue = segment.Value;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("Segment value not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                }
                else
                {
                    throw new HL7Exception("Segment name not available: " + strValueFormat);
                }
            }
            else
            {
                throw new HL7Exception("Request format is not valid: " + strValueFormat);
            }

            return strValue;
        }

        /// <summary>
        /// Sets the Value of specific Field/Component/SubCpomponent, throws error if field/component index is not valid
        /// </summary>
        /// <param name="strValueFormat">Field/Component position in format SEGMENTNAME.FieldIndex.ComponentIndex.SubComponentIndex example PID.5.2</param>
        /// <param name="strValue">Value for the specified field/component</param>
        /// <returns>boolean</returns>
        public bool SetValue(string strValueFormat, string strValue)
        {
            var allComponents = MessageHelper.SplitString(strValueFormat, new char[] { '.' });
            var comCount = allComponents.Count;
            var isValid = ValidateValueFormat(allComponents);

            bool isSet;
            if (isValid)
            {
                var segmentName = allComponents[0];

                if (SegmentList.ContainsKey(segmentName))
                {
                    var segment = SegmentList[segmentName].First();

                    int componentIndex;
                    if (comCount == 4)
                    {
                        _ = int.TryParse(allComponents[2], out componentIndex);
                        _ = int.TryParse(allComponents[3], out var subComponentIndex);

                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            field.ComponentList[componentIndex - 1].SubComponentList[subComponentIndex - 1].Value = strValue;
                            isSet = true;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("SubComponent not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else if (comCount == 3)
                    {
                        _ = int.TryParse(allComponents[2], out componentIndex);

                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            field.ComponentList[componentIndex - 1].Value = strValue;
                            isSet = true;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("Component not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else if (comCount == 2)
                    {
                        try
                        {
                            var field = GetField(segment, allComponents[1]);
                            field.Value = strValue;
                            isSet = true;
                        }
                        catch (Exception ex)
                        {
                            throw new HL7Exception("Field not available - " + strValueFormat + " Error: " + ex.Message);
                        }
                    }
                    else
                    {
                        throw new HL7Exception("Cannot overwrite a segment value");
                    }
                }
                else
                    throw new HL7Exception("Segment name not available");
            }
            else
                throw new HL7Exception("Request format is not valid");

            return isSet;
        }

        /// <summary>
        /// check if specified field has components
        /// </summary>
        /// <param name="strValueFormat">Field/Component position in format SEGMENTNAME.FieldIndex.ComponentIndex.SubComponentIndex example PID.5.2</param>
        /// <returns>boolean</returns>
        public bool IsComponentized(string strValueFormat)
        {
            var allComponents = MessageHelper.SplitString(strValueFormat, new char[] { '.' });
            var isValid = ValidateValueFormat(allComponents);

            if (isValid)
            {
                var segmentName = allComponents[0];
                if (allComponents.Count >= 2)
                {
                    try
                    {
                        var segment = SegmentList[segmentName].First();
                        var field = GetField(segment, allComponents[1]);

                        return field.IsComponentized;
                    }
                    catch (Exception ex)
                    {
                        throw new HL7Exception("Field not available - " + strValueFormat + " Error: " + ex.Message);
                    }
                }
                else
                    throw new HL7Exception("Field not identified in request");
            }
            else
                throw new HL7Exception("Request format is not valid");

        }

        /// <summary>
        /// check if specified fields has repeatitions
        /// </summary>
        /// <param name="strValueFormat">Field/Component position in format SEGMENTNAME.FieldIndex.ComponentIndex.SubComponentIndex example PID.5.2</param>
        /// <returns>boolean</returns>
        public bool HasRepetitions(string strValueFormat)
        {
            var allComponents = MessageHelper.SplitString(strValueFormat, new char[] { '.' });
            var isValid = ValidateValueFormat(allComponents);

            if (isValid)
            {
                var segmentName = allComponents[0];

                if (allComponents.Count >= 2)
                {
                    try
                    {
                        var segment = SegmentList[segmentName].First();
                        var field = GetField(segment, allComponents[1]);

                        return field.HasRepetitions;
                    }
                    catch (Exception ex)
                    {
                        throw new HL7Exception("Field not available - " + strValueFormat + " Error: " + ex.Message);
                    }
                }
                else
                    throw new HL7Exception("Field not identified in request");
            }
            else
                throw new HL7Exception("Request format is not valid");
        }

        /// <summary>
        /// check if specified component has sub components
        /// </summary>
        /// <param name="strValueFormat">Field/Component position in format SEGMENTNAME.FieldIndex.ComponentIndex.SubComponentIndex example PID.5.2</param>
        /// <returns>boolean</returns>
        public bool IsSubComponentized(string strValueFormat)
        {
            var allComponents = MessageHelper.SplitString(strValueFormat, new char[] { '.' });
            var isValid = ValidateValueFormat(allComponents);

            if (isValid)
            {
                var segmentName = allComponents[0];

                if (allComponents.Count >= 3)
                {
                    try
                    {
                        var segment = SegmentList[segmentName].First();
                        var field = GetField(segment, allComponents[1]);
                        _ = int.TryParse(allComponents[2], out var componentIndex);
                        return field.ComponentList[componentIndex - 1].IsSubComponentized;
                    }
                    catch (Exception ex)
                    {
                        throw new HL7Exception("Component not available - " + strValueFormat + " Error: " + ex.Message);
                    }
                }
                else
                    throw new HL7Exception("Component not identified in request");
            }
            else
                throw new HL7Exception("Request format is not valid");

        }

        /// <summary>
        /// Builds the acknowledgement message for this message
        /// </summary>
        /// <returns>An ACK message if success, otherwise null</returns>
        public Message GetACK()
        {
            return CreateAckMessage("AA", false, null);
        }

        /// <summary>
        /// Builds a negative ack for this message
        /// </summary>
        /// <param name="code">ack code like AR, AE</param>
        /// <param name="errMsg">Error message to be sent with NACK</param>
        /// <returns>A NACK message if success, otherwise null</returns>
        public Message GetNACK(string code, string errMsg)
        {
            return CreateAckMessage(code, true, errMsg);
        }

        /// <summary>
        /// Adds a segment to the message
        /// </summary>
        /// <param name="newSegment">Segment to be appended to the end of the message</param>
        /// <returns>True if added successfully, otherwise false</returns>
        public bool AddNewSegment(Segment newSegment)
        {
            try
            {
                newSegment.SequenceNo = SegmentCount++;

                if (!SegmentList.ContainsKey(newSegment.Name))
                    SegmentList[newSegment.Name] = new List<Segment>();

                SegmentList[newSegment.Name].Add(newSegment);
                return true;
            }
            catch (Exception ex)
            {
                SegmentCount--;
                throw new HL7Exception("Unable to add new segment. Error - " + ex.Message);
            }
        }

        /// <summary>
        /// Removes a segment from the message
        /// </summary>
        /// <param name="segmentName">Segment to be removed</param>
        /// <param name="index">Zero-based index of the segment to be removed, in case of multiple. Default is 0.</param>
        /// <returns>True if found and removed successfully, otherwise false</returns>
        public bool RemoveSegment(string segmentName, int index = 0)
        {
            try
            {
                if (!SegmentList.ContainsKey(segmentName))
                    return false;

                var list = SegmentList[segmentName];
                if (list.Count <= index)
                    return false;

                list.RemoveAt(index);
                SegmentCount--;

                return true;
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Unable to add remove segment. Error - " + ex.Message);
            }
        }

        public List<Segment> Segments()
        {
            return GetAllSegmentsInOrder();
        }

        public List<Segment> Segments(string segmentName)
        {
            return GetAllSegmentsInOrder().FindAll(o => o.Name.Equals(segmentName));
        }

        public Segment DefaultSegment(string segmentName)
        {
            return GetAllSegmentsInOrder().First(o => o.Name.Equals(segmentName));
        }

        /// <summary>
        /// Addsthe header segment to a new message
        /// </summary>
        /// <param name="sendingApplication">Sending application name</param>
        /// <param name="sendingFacility">Sending facility name</param>
        /// <param name="receivingApplication">Receiving application name</param>
        /// <param name="receivingFacility">Receiving facility name</param>
        /// <param name="security">Security features. Can be null.</param>
        /// <param name="messageType">Message type ^ trigger event</param>
        /// <param name="messageControlID">Message control unique ID</param>
        /// <param name="processingID">Processing ID ^ processing mode</param>
        /// <param name="version">HL7 message version (2.x)</param>
        public void AddSegmentMSH(string sendingApplication, string sendingFacility, string receivingApplication, string receivingFacility,
            string security, string messageType, string messageControlID, string processingID, string version)
        {
            var dateString = MessageHelper.LongDateWithFractionOfSecond(DateTime.Now);
            var delim = Encoding.FieldDelimiter;

            var response = "MSH" + Encoding.AllDelimiters + delim + sendingApplication + delim + sendingFacility + delim
                + receivingApplication + delim + receivingFacility + delim
                + dateString + delim + (security ?? string.Empty) + delim + messageType + delim + messageControlID + delim
                + processingID + delim + version + Encoding.SegmentDelimiter;

            var message = new Message(response);
            message.ParseMessage();
            AddNewSegment(message.DefaultSegment("MSH"));
        }

        /// <summary>
        /// Serialize to MLLP escaped byte array
        /// </summary>
        /// <param name="validate">Optional. Validate the message before serializing</param>
        /// <returns>MLLP escaped byte array</returns>
        public byte[] GetMLLP(bool validate = false)
        {
            var hl7 = SerializeMessage(validate);

            return MessageHelper.GetMLLP(hl7);
        }

        /// <summary>
        /// Builds an ACK or NACK message for this message
        /// </summary>
        /// <param name="code">ack code like AA, AR, AE</param>
        /// <param name="isNack">true for generating a NACK message, otherwise false</param>
        /// <param name="errMsg">error message to be sent with NACK</param>
        /// <returns>An ACK or NACK message if success, otherwise null</returns>
        private Message CreateAckMessage(string code, bool isNack, string errMsg)
        {
            var response = new StringBuilder();

            if (MessageStructure != "ACK")
            {
                var dateString = MessageHelper.LongDateWithFractionOfSecond(DateTime.Now);
                var msh = SegmentList["MSH"].First();
                var delim = Encoding.FieldDelimiter;

                response.Append("MSH").Append(Encoding.AllDelimiters).Append(delim).Append(msh.FieldList[4].Value).Append(delim).Append(msh.FieldList[5].Value).Append(delim)
                    .Append(msh.FieldList[2].Value).Append(delim).Append(msh.FieldList[3].Value).Append(delim)
                    .Append(dateString).Append(delim).Append(delim).Append("ACK").Append(delim).Append(MessageControlID).Append(delim)
                    .Append(ProcessingID).Append(delim).Append(Version).Append(Encoding.SegmentDelimiter);

                response.Append("MSA").Append(delim).Append(code).Append(delim).Append(MessageControlID).Append((isNack ? delim + errMsg : string.Empty)).Append(Encoding.SegmentDelimiter);
            }
            else
            {
                return null;
            }

            try
            {
                var message = new Message(response.ToString());
                message.ParseMessage();
                return message;
            }
            catch
            {
                return null;
            }
        }


        private static Field GetField(Segment segment, string index)
        {
            var repetition = 0;
            var matches = Regex.Matches(index, FieldRegex);

            if (matches.Count < 1)
                throw new Exception("Invalid field index");

            _ = int.TryParse(matches[0].Groups[1].Value, out var fieldIndex);
            fieldIndex--;

            if (matches[0].Length > 3)
            {
                _ = int.TryParse(matches[0].Groups[3].Value, out repetition);
                repetition--;
            }

            var field = segment.FieldList[fieldIndex];

            if (field.HasRepetitions)
                field = field.RepeatitionList[repetition];

            return field;
        }

        /// <summary>
        /// Validates the HL7 message for basic syntax
        /// </summary>
        /// <returns>A boolean indicating whether the whole message is valid or not</returns>
        private bool ValidateMessage()
        {
            try
            {
                if (!string.IsNullOrEmpty(HL7Message))
                {
                    // Check message length - MSH+Delimeters+12Fields in MSH
                    if (HL7Message.Length < 20)
                    {
                        throw new HL7Exception("Message Length too short: " + HL7Message.Length + " chars.", HL7Exception.BAD_MESSAGE);
                    }

                    // Check if message starts with header segment
                    if (!HL7Message.StartsWith("MSH"))
                    {
                        throw new HL7Exception("MSH segment not found at the beggining of the message", HL7Exception.BAD_MESSAGE);
                    }

                    Encoding.EvaluateSegmentDelimiter(HL7Message);
                    HL7Message = string.Join(Encoding.SegmentDelimiter, MessageHelper.SplitMessage(HL7Message)) + Encoding.SegmentDelimiter;

                    // Check Segment Name & 4th character of each segment
                    var fourthCharMSH = HL7Message[3];
                    _allSegments = MessageHelper.SplitMessage(HL7Message);

                    foreach (var strSegment in _allSegments)
                    {
                        if (string.IsNullOrWhiteSpace(strSegment))
                            continue;

                        var segmentName = strSegment.Substring(0, 3);
                        var isValidSegmentName = System.Text.RegularExpressions.Regex.IsMatch(segmentName, SegmentRegex);

                        if (!isValidSegmentName)
                        {
                            throw new HL7Exception("Invalid segment name found: " + strSegment, HL7Exception.BAD_MESSAGE);
                        }

                        if (strSegment.Length > 3 && fourthCharMSH != strSegment[3])
                        {
                            throw new HL7Exception("Invalid segment found: " + strSegment, HL7Exception.BAD_MESSAGE);
                        }
                    }

                    var fieldDelimiters_Message = _allSegments[0].Substring(3, 8 - 3);
                    Encoding.EvaluateDelimiters(fieldDelimiters_Message);

                    // Count field separators, MSH.12 is required so there should be at least 11 field separators in MSH
                    var countFieldSepInMSH = _allSegments[0].Count(f => f == Encoding.FieldDelimiter);

                    if (countFieldSepInMSH < 11)
                    {
                        throw new HL7Exception("MSH segment doesn't contain all the required fields", HL7Exception.BAD_MESSAGE);
                    }

                    // Find Message Version
                    var mshFields = MessageHelper.SplitString(_allSegments[0], Encoding.FieldDelimiter);

                    Version = mshFields.Count >= 12
                        ? MessageHelper.SplitString(mshFields[11], Encoding.ComponentDelimiter)[0]
                        : throw new HL7Exception("HL7 version not found in the MSH segment", HL7Exception.REQUIRED_FIELD_MISSING);

                    // Find Message Type & Trigger Event
                    try
                    {
                        var msh_9 = mshFields[8];

                        if (!string.IsNullOrEmpty(msh_9))
                        {
                            var msh_9_comps = MessageHelper.SplitString(msh_9, Encoding.ComponentDelimiter);

                            if (msh_9_comps.Count >= 3)
                            {
                                MessageStructure = msh_9_comps[2];
                            }
                            else if (msh_9_comps.Count > 0 && msh_9_comps[0] != null && msh_9_comps[0].Equals("ACK"))
                            {
                                MessageStructure = "ACK";
                            }
                            else if (msh_9_comps.Count == 2)
                            {
                                MessageStructure = msh_9_comps[0] + "_" + msh_9_comps[1];
                            }
                            else
                            {
                                throw new HL7Exception("Message Type & Trigger Event value not found in message", HL7Exception.UNSUPPORTED_MESSAGE_TYPE);
                            }
                        }
                        else
                            throw new HL7Exception("MSH.10 not available", HL7Exception.UNSUPPORTED_MESSAGE_TYPE);
                    }
                    catch (System.IndexOutOfRangeException e)
                    {
                        throw new HL7Exception("Can't find message structure (MSH.9.3) - " + e.Message, HL7Exception.UNSUPPORTED_MESSAGE_TYPE);
                    }

                    try
                    {
                        MessageControlID = mshFields[9];

                        if (string.IsNullOrEmpty(MessageControlID))
                            throw new HL7Exception("MSH.10 - Message Control ID not found", HL7Exception.REQUIRED_FIELD_MISSING);
                    }
                    catch (Exception ex)
                    {
                        throw new HL7Exception("Error occured while accessing MSH.10 - " + ex.Message, HL7Exception.REQUIRED_FIELD_MISSING);
                    }

                    try
                    {
                        ProcessingID = mshFields[10];

                        if (string.IsNullOrEmpty(ProcessingID))
                            throw new HL7Exception("MSH.11 - Processing ID not found", HL7Exception.REQUIRED_FIELD_MISSING);
                    }
                    catch (Exception ex)
                    {
                        throw new HL7Exception("Error occured while accessing MSH.11 - " + ex.Message, HL7Exception.REQUIRED_FIELD_MISSING);
                    }
                }
                else
                    throw new HL7Exception("No Message Found", HL7Exception.BAD_MESSAGE);
            }
            catch (Exception ex)
            {
                throw new HL7Exception("Failed to validate the message with error - " + ex.Message, HL7Exception.BAD_MESSAGE);
            }

            return true;
        }

        /// <summary>
        /// Serializes a field into a string with proper encoding
        /// </summary>
        /// <returns>A serialized string</returns>
        private void SerializeField(Field field, StringBuilder strMessage)
        {
            if (field.ComponentList.Count > 0)
            {
                var indexCom = 0;

                foreach (var com in field.ComponentList)
                {
                    indexCom++;
                    if (com.SubComponentList.Count > 0)
                        strMessage.Append(string.Join(Encoding.SubComponentDelimiter.ToString(), com.SubComponentList.Select(sc => Encoding.Encode(sc.Value))));
                    else
                        strMessage.Append(Encoding.Encode(com.Value));

                    if (indexCom < field.ComponentList.Count)
                        strMessage.Append(Encoding.ComponentDelimiter);
                }
            }
            else
                strMessage.Append(Encoding.Encode(field.Value));

        }

        /// <summary> 
        /// Get all segments in order as they appear in original message. This the usual order: IN1|1 IN2|1 IN1|2 IN2|2
        /// </summary>
        /// <returns>A list of segments in the proper order</returns>
        private List<Segment> GetAllSegmentsInOrder()
        {
            var list = new List<Segment>();

            foreach (var segName in SegmentList.Keys)
            {
                foreach (var seg in SegmentList[segName])
                {
                    list.Add(seg);
                }
            }

            return list.OrderBy(o => o.SequenceNo).ToList();
        }

        /// <summary>
        /// Validates the components of a value's position descriptor
        /// </summary>
        /// <returns>A boolean indicating whether all the components are valid or not</returns>
        private static bool ValidateValueFormat(List<string> allComponents)
        {
            var isValid = false;

            if (allComponents.Count > 0)
            {
                if (Regex.IsMatch(allComponents[0], SegmentRegex))
                {
                    for (var i = 1; i < allComponents.Count; i++)
                    {
                        if (i == 1 && Regex.IsMatch(allComponents[i], FieldRegex))
                            isValid = true;
                        else if (i > 1 && Regex.IsMatch(allComponents[i], OtherRegEx))
                            isValid = true;
                        else
                            return false;
                    }
                }
                else
                {
                    isValid = false;
                }
            }

            return isValid;
        }
    }
}
