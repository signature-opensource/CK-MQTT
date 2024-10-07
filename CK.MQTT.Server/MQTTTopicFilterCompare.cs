using System;
using System.Net;

namespace CK.MQTT.Server;

public static class MQTTTopicFilterComparer
{
    public const char LevelSeparator = '/';
    public const char MultiLevelWildcard = '#';
    public const char SingleLevelWildcard = '+';
    public const char ReservedTopicPrefix = '$';

    public static bool IsMatch( string topic, string filter )
    {
        var filterOffset = 0;
        var filterLength = filter.Length;

        var topicOffset = 0;
        var topicLength = topic.Length;
        ReadOnlySpan<char> topicPointer = topic;
        ReadOnlySpan<char> filterPointer = filter;
        if( filterLength > topicLength )
        {
            // It is impossible to create a filter which is longer than the actual topic.
            // The only way this can happen is when the last char is a wildcard char.
            // sensor/7/temperature >> sensor/7/temperature = Equal
            // sensor/+/temperature >> sensor/7/temperature = Equal
            // sensor/7/+           >> sensor/7/temperature = Shorter
            // sensor/#             >> sensor/7/temperature = Shorter
            var lastFilterChar = filterPointer[filterLength - 1];
            if( lastFilterChar != MultiLevelWildcard && lastFilterChar != SingleLevelWildcard )
            {
                return false;
            }
        }

        var isMultiLevelFilter = filterPointer[filterLength - 1] == MultiLevelWildcard;
        var isReservedTopic = topicPointer[0] == ReservedTopicPrefix;

        if( isReservedTopic && filterLength == 1 && isMultiLevelFilter )
        {
            // It is not allowed to receive i.e. '$foo/bar' with filter '#'.
            return false;
        }

        if( isReservedTopic && filterPointer[0] == SingleLevelWildcard )
        {
            // It is not allowed to receive i.e. '$SYS/monitor/Clients' with filter '+/monitor/Clients'.
            return false;
        }

        if( filterLength == 1 && isMultiLevelFilter )
        {
            // Filter '#' matches basically everything.
            return true;
        }

        // Go through the filter char by char.
        while( filterOffset < filterLength && topicOffset < topicLength )
        {
            // Check if the current char is a multi level wildcard. The char is only allowed
            // at the very las position.
            if( filterPointer[filterOffset] == MultiLevelWildcard && filterOffset != filterLength - 1 )
            {
                throw new ProtocolViolationException();
            }

            if( filterPointer[filterOffset] == topicPointer[topicOffset] )
            {
                if( topicOffset == topicLength - 1 )
                {
                    // Check for e.g. "foo" matching "foo/#"
                    if( filterOffset == filterLength - 3 && filterPointer[filterOffset + 1] == LevelSeparator && isMultiLevelFilter )
                    {
                        return true;
                    }

                    // Check for e.g. "foo/" matching "foo/#"
                    if( filterOffset == filterLength - 2 && filterPointer[filterOffset] == LevelSeparator && isMultiLevelFilter )
                    {
                        return true;
                    }
                }

                filterOffset++;
                topicOffset++;

                // Check if the end was reached and i.e. "foo/bar" matches "foo/bar"
                if( filterOffset == filterLength && topicOffset == topicLength )
                {
                    return true;
                }

                var endOfTopic = topicOffset == topicLength;

                if( endOfTopic && filterOffset == filterLength - 1 && filterPointer[filterOffset] == SingleLevelWildcard )
                {
                    if( filterOffset > 0 && filterPointer[filterOffset - 1] != LevelSeparator )
                    {
                        throw new ProtocolViolationException( "Invalid filter" );
                    }
                    return true;
                }
            }
            else
            {
                if( filterPointer[filterOffset] == SingleLevelWildcard )
                {
                    // Check for invalid "+foo" or "a/+foo" subscription
                    if( filterOffset > 0 && filterPointer[filterOffset - 1] != LevelSeparator )
                    {
                        throw new ProtocolViolationException( "Invalid filter" );
                    }

                    // Check for bad "foo+" or "foo+/a" subscription
                    if( filterOffset < filterLength - 1 && filterPointer[filterOffset + 1] != LevelSeparator )
                    {
                        throw new ProtocolViolationException( "Invalid filter" );
                    }

                    filterOffset++;
                    while( topicOffset < topicLength && topicPointer[topicOffset] != LevelSeparator )
                    {
                        topicOffset++;
                    }

                    if( topicOffset == topicLength && filterOffset == filterLength )
                    {
                        return true;
                    }
                }
                else if( filterPointer[filterOffset] == MultiLevelWildcard )
                {
                    if( filterOffset > 0 && filterPointer[filterOffset - 1] != LevelSeparator )
                    {
                        throw new ProtocolViolationException( "Invalid filter" );
                    }

                    if( filterOffset + 1 != filterLength )
                    {
                        throw new ProtocolViolationException( "Invalid filter" );
                    }

                    return true;
                }
                else
                {
                    // Check for e.g. "foo/bar" matching "foo/+/#".
                    if( filterOffset > 0 && filterOffset + 2 == filterLength && topicOffset == topicLength && filterPointer[filterOffset - 1] == SingleLevelWildcard &&
                        filterPointer[filterOffset] == LevelSeparator && isMultiLevelFilter )
                    {
                        return true;
                    }

                    return false;
                }
            }
        }
        return false;
    }
}
