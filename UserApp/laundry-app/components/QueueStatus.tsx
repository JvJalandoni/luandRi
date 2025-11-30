import React, { useState, useEffect } from 'react';
import { View, Text, StyleSheet, ActivityIndicator } from 'react-native';
import { laundryService } from '../services/laundryService';
import { useThemeColor } from '../hooks/useThemeColor';
import { ThemedText } from './ThemedText';
import { Clock, Users } from 'lucide-react-native';

interface QueueStatusProps {
  requestId: number;
  refreshInterval?: number; // milliseconds
}

export function QueueStatus({ requestId, refreshInterval = 10000 }: QueueStatusProps) {
  const [queueData, setQueueData] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const cardColor = useThemeColor({}, 'card');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');
  const warningColor = '#f59e0b';

  const fetchQueueStatus = async () => {
    try {
      const data = await laundryService.getQueueStatus(requestId);
      setQueueData(data);
      setError(null);
    } catch (err: any) {
      console.error('Error fetching queue status:', err);
      setError('Failed to load queue status');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchQueueStatus();

    // Set up polling for real-time updates
    const interval = setInterval(fetchQueueStatus, refreshInterval);

    return () => clearInterval(interval);
  }, [requestId, refreshInterval]);

  if (loading) {
    return (
      <View style={[styles.container, { backgroundColor: cardColor, borderColor }]}>
        <ActivityIndicator size="small" color={primaryColor} />
        <ThemedText style={styles.loadingText}>Loading queue status...</ThemedText>
      </View>
    );
  }

  if (error) {
    return null; // Don't show error, just hide component
  }

  if (!queueData || !queueData.isInQueue) {
    return null; // Not in queue, don't show component
  }

  const formatWaitTime = (minutes: number): string => {
    if (minutes === 0) return 'Less than a minute';
    if (minutes < 60) return `~${minutes} minute${minutes !== 1 ? 's' : ''}`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    if (mins === 0) return `~${hours} hour${hours !== 1 ? 's' : ''}`;
    return `~${hours}h ${mins}m`;
  };

  const isNextInLine = queueData.queuePosition === 1;

  return (
    <View style={[
      styles.container, 
      { 
        backgroundColor: isNextInLine ? primaryColor + '15' : cardColor, 
        borderColor: isNextInLine ? primaryColor : borderColor 
      }
    ]}>
      <View style={styles.header}>
        <View style={styles.iconContainer}>
          <Users size={24} color={isNextInLine ? primaryColor : warningColor} />
        </View>
        <ThemedText style={[styles.title, { color: isNextInLine ? primaryColor : warningColor }]}>
          {isNextInLine ? '🎯 You\'re Next in Line!' : '⏳ In Queue'}
        </ThemedText>
      </View>

      <View style={styles.content}>
        <View style={styles.infoRow}>
          <View style={styles.infoItem}>
            <ThemedText style={[styles.label, { color: mutedColor }]}>Position</ThemedText>
            <ThemedText style={[styles.value, { color: textColor }]}>
              #{queueData.queuePosition}
            </ThemedText>
          </View>

          <View style={styles.divider} />

          <View style={styles.infoItem}>
            <ThemedText style={[styles.label, { color: mutedColor }]}>Total in Queue</ThemedText>
            <ThemedText style={[styles.value, { color: textColor }]}>
              {queueData.totalInQueue}
            </ThemedText>
          </View>
        </View>

        <View style={[styles.waitTimeContainer, { backgroundColor: cardColor + '80' }]}>
          <Clock size={18} color={primaryColor} />
          <View style={styles.waitTimeText}>
            <ThemedText style={[styles.waitLabel, { color: mutedColor }]}>
              Estimated Wait Time
            </ThemedText>
            <ThemedText style={[styles.waitValue, { color: textColor }]}>
              {formatWaitTime(queueData.estimatedWaitTimeMinutes)}
            </ThemedText>
          </View>
        </View>

        <ThemedText style={[styles.message, { color: mutedColor }]}>
          {queueData.message}
        </ThemedText>

        {isNextInLine && (
          <View style={[styles.nextUpBadge, { backgroundColor: primaryColor }]}>
            <Text style={styles.nextUpText}>Get ready! You'll be served soon.</Text>
          </View>
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: 16,
    borderWidth: 2,
    padding: 16,
    marginVertical: 12,
  },
  loadingText: {
    textAlign: 'center',
    marginTop: 8,
    fontSize: 14,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    marginBottom: 16,
  },
  iconContainer: {
    marginRight: 12,
  },
  title: {
    fontSize: 18,
    fontWeight: '700',
  },
  content: {
    gap: 12,
  },
  infoRow: {
    flexDirection: 'row',
    justifyContent: 'space-around',
    alignItems: 'center',
    paddingVertical: 12,
  },
  infoItem: {
    alignItems: 'center',
    flex: 1,
  },
  label: {
    fontSize: 12,
    marginBottom: 4,
    textTransform: 'uppercase',
    letterSpacing: 0.5,
  },
  value: {
    fontSize: 24,
    fontWeight: '700',
  },
  divider: {
    width: 1,
    height: 40,
    backgroundColor: '#e5e5e5',
  },
  waitTimeContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: 12,
    borderRadius: 12,
    gap: 10,
  },
  waitTimeText: {
    flex: 1,
  },
  waitLabel: {
    fontSize: 12,
    marginBottom: 2,
  },
  waitValue: {
    fontSize: 16,
    fontWeight: '600',
  },
  message: {
    fontSize: 13,
    textAlign: 'center',
    fontStyle: 'italic',
  },
  nextUpBadge: {
    padding: 12,
    borderRadius: 8,
    marginTop: 4,
  },
  nextUpText: {
    color: '#ffffff',
    fontSize: 14,
    fontWeight: '600',
    textAlign: 'center',
  },
});
