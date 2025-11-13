import React, { useEffect, useState , useCallback } from 'react';
import {
  View,
  Text,
  StyleSheet,
  FlatList,
  TouchableOpacity,
  RefreshControl,
  ActivityIndicator,
  TextInput,
  ScrollView,
} from 'react-native';
import { laundryService, LaundryRequestResponse } from '../../services/laundryService';
import { useThemeColor } from '../../hooks/useThemeColor';
import { ThemedView } from '../../components/ThemedView';
import { ThemedText } from '../../components/ThemedText';
import { useRouter, useFocusEffect } from 'expo-router';
import { formatRelativeTime } from '../../utils/dateUtils';
import { useCustomAlert } from '../../components/CustomAlert';

/**
 * History Screen - Displays customer's complete laundry request history
 * Features:
 * - List of all past and current requests sorted by date (newest first)
 * - Status badges with color coding (completed, cancelled, declined, etc.)
 * - Tap request to view full details
 * - Pull-to-refresh support
 * - Auto-refresh when screen gains focus
 * - Empty state message when no requests exist
 *
 * @returns React component displaying request history list
 */
export default function HistoryScreen() {
  const router = useRouter();
  const [allRequests, setAllRequests] = useState<LaundryRequestResponse[]>([]);
  const [filteredRequests, setFilteredRequests] = useState<LaundryRequestResponse[]>([]);
  const [displayedRequests, setDisplayedRequests] = useState<LaundryRequestResponse[]>([]);
  const [isLoading, setIsLoading] = useState(false);
  const [isLoadingMore, setIsLoadingMore] = useState(false);
  const { showAlert, AlertComponent } = useCustomAlert();

  // Filter states
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState<string>('');

  // Pagination states
  const [currentPage, setCurrentPage] = useState(1);
  const [itemsPerPage] = useState(10);
  const [hasMore, setHasMore] = useState(false);

  const backgroundColor = useThemeColor({}, 'background');
  const textColor = useThemeColor({}, 'text');
  const primaryColor = useThemeColor({}, 'primary');
  const secondaryColor = useThemeColor({}, 'secondary');
  const cardColor = useThemeColor({}, 'card');
  const borderColor = useThemeColor({}, 'border');
  const mutedColor = useThemeColor({}, 'muted');
  const dangerColor = useThemeColor({}, 'danger');
  const warningColor = useThemeColor({}, 'warning');

  /**
   * Loads all laundry requests for the current user from the server
   * Sorts requests by date (newest first) and updates state
   * Handles errors with alert display
   */
  const loadRequests = async () => {
    try {
      setIsLoading(true);
      const userRequests = await laundryService.getUserRequests();
      const sorted = userRequests.sort((a, b) => new Date(b.requestedAt || 0).getTime() - new Date(a.requestedAt || 0).getTime());
      setAllRequests(sorted);
      setCurrentPage(1); // Reset to first page
    } catch (error: any) {
      showAlert('Error', 'Failed to load request history');
    } finally {
      setIsLoading(false);
    }
  };

  // Apply filters when allRequests, statusFilter, or searchQuery changes
  useEffect(() => {
    let filtered = [...allRequests];

    // Apply status filter
    if (statusFilter !== 'all') {
      filtered = filtered.filter(req => getStatusString(req.status) === statusFilter);
    }

    // Apply search filter (search by request ID or robot name)
    if (searchQuery.trim()) {
      const query = searchQuery.toLowerCase().trim();
      filtered = filtered.filter(req =>
        req.id.toString().includes(query) ||
        (req.assignedRobot && req.assignedRobot.toLowerCase().includes(query))
      );
    }

    setFilteredRequests(filtered);
    setCurrentPage(1); // Reset to first page when filters change
  }, [allRequests, statusFilter, searchQuery]);

  // Apply pagination when filteredRequests or currentPage changes
  useEffect(() => {
    const startIndex = 0;
    const endIndex = currentPage * itemsPerPage;
    const paginatedData = filteredRequests.slice(startIndex, endIndex);

    setDisplayedRequests(paginatedData);
    setHasMore(endIndex < filteredRequests.length);
  }, [filteredRequests, currentPage, itemsPerPage]);

  const loadMore = () => {
    if (!isLoadingMore && hasMore) {
      setIsLoadingMore(true);
      setTimeout(() => {
        setCurrentPage(prev => prev + 1);
        setIsLoadingMore(false);
      }, 300);
    }
  };

  useEffect(() => {
    loadRequests();
  }, []);

  // Refresh data when screen comes into focus
  useFocusEffect(
    useCallback(() => {
      loadRequests();
    }, [])
  );

  /**
   * Converts backend status enum to lowercase string for display and logic
   * Maps numeric status codes (0-11) to readable status strings
   * @param status - Numeric status code from backend
   * @returns Lowercase status string (e.g., 'completed', 'pending', 'cancelled')
   */
  const getStatusString = (status: any): string => {
    // Convert backend enum to string
    switch (Number(status)) {
      case 0: return 'pending';
      case 1: return 'accepted';
      case 2: return 'inprogress';
      case 3: return 'robotenroute';
      case 4: return 'arrivedatroom';
      case 5: return 'laundryloaded';
      case 6: return 'returnedtobase';
      case 7: return 'weighingcomplete';
      case 8: return 'paymentpending';
      case 9: return 'completed';
      case 10: return 'declined';
      case 11: return 'cancelled';
      default: return String(status).toLowerCase();
    }
  };

  /**
   * Returns appropriate color for status badge based on request status
   * Completed/accepted = primary, pending = warning, declined/cancelled = danger
   * @param status - Request status code
   * @returns Theme color for status badge
   */
  const getStatusColor = (status: any) => {
    const statusStr = getStatusString(status);
    switch (statusStr) {
      case 'pending': return warningColor;
      case 'accepted': return primaryColor;
      case 'inprogress': return primaryColor;
      case 'completed': return secondaryColor;
      case 'declined': return dangerColor;
      case 'cancelled': return mutedColor;
      default: return mutedColor;
    }
  };

  const getRequestTypeLabel = (type: number) => {
    switch (type) {
      case 0: return 'Pickup Only';
      case 1: return 'Delivery Only';
      case 2: return 'Pickup & Delivery';
      default: return 'Unknown';
    }
  };

  const handleViewRequest = (requestId: number) => {
    router.push(`/request-details?requestId=${requestId}`);
  };

  const renderRequestCard = ({ item: request }: { item: LaundryRequestResponse }) => (
    <View style={[styles.requestCard, { backgroundColor: cardColor, borderColor: borderColor }]}>
      <View style={styles.requestHeader}>
        <View>
          <ThemedText style={styles.requestId}>Request #{request.id}</ThemedText>
          <ThemedText style={[styles.requestType, { color: mutedColor }]}>
            {getRequestTypeLabel(request.type || 2)}
          </ThemedText>
        </View>
        <View style={[styles.statusBadge, { backgroundColor: getStatusColor(request.status) }]}>
          <Text style={styles.statusText}>{request.status}</Text>
        </View>
      </View>

      <View style={styles.requestDetails}>
        <View style={styles.detailRow}>
          <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Requested:</ThemedText>
          <ThemedText style={[styles.detailValue, { color: textColor }]}>
            {formatRelativeTime(request.requestedAt)}
          </ThemedText>
        </View>
        <View style={styles.detailRow}>
          <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Scheduled:</ThemedText>
          <ThemedText style={[styles.detailValue, { color: textColor }]}>
            {formatRelativeTime(request.scheduledAt)}
          </ThemedText>
        </View>
        {request.weight && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Weight:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>{request.weight}kg</ThemedText>
          </View>
        )}
        {request.totalCost && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Cost:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: secondaryColor }]}>₱{request.totalCost}</ThemedText>
          </View>
        )}
        {request.assignedRobot && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Robot:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>{request.assignedRobot}</ThemedText>
          </View>
        )}
        {request.completedAt && (
          <View style={styles.detailRow}>
            <ThemedText style={[styles.detailLabel, { color: mutedColor }]}>Completed:</ThemedText>
            <ThemedText style={[styles.detailValue, { color: textColor }]}>
              {formatRelativeTime(request.completedAt)}
            </ThemedText>
          </View>
        )}
        {request.declineReason && (
          <View style={[styles.declineReason, { backgroundColor: dangerColor + '20', borderLeftColor: dangerColor }]}>
            <ThemedText style={[styles.declineLabel, { color: dangerColor }]}>Decline Reason:</ThemedText>
            <ThemedText style={[styles.declineText, { color: dangerColor }]}>{request.declineReason}</ThemedText>
          </View>
        )}
      </View>

      <TouchableOpacity
        style={[styles.trackButton, { backgroundColor: cardColor, borderColor: borderColor }]}
        onPress={() => handleViewRequest(request.id)}
      >
        <ThemedText style={[styles.trackButtonText, { color: primaryColor }]}>View Details</ThemedText>
      </TouchableOpacity>
    </View>
  );

  const renderHeader = () => (
    <View style={styles.header}>
      <ThemedText style={styles.title}>Request History</ThemedText>
      <ThemedText style={[styles.subtitle, { color: mutedColor }]}>
        {filteredRequests.length} {filteredRequests.length === 1 ? 'request' : 'requests'}
        {statusFilter !== 'all' && ` (filtered by ${statusFilter})`}
      </ThemedText>

      {/* Search Bar */}
      <View style={[styles.searchContainer, { backgroundColor: cardColor, borderColor: borderColor }]}>
        <TextInput
          style={[styles.searchInput, { color: textColor }]}
          placeholder="Search by request ID or robot..."
          placeholderTextColor={mutedColor}
          value={searchQuery}
          onChangeText={setSearchQuery}
        />
      </View>

      {/* Status Filter Chips */}
      <View style={styles.filterChipsContainer}>
        <ThemedText style={[styles.filterLabel, { color: mutedColor }]}>Filter:</ThemedText>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterChipsScroll}>
          {['all', 'pending', 'accepted', 'inprogress', 'washing', 'completed', 'cancelled', 'declined'].map((status) => (
            <TouchableOpacity
              key={status}
              style={[
                styles.filterChip,
                { backgroundColor: statusFilter === status ? primaryColor : cardColor, borderColor: borderColor }
              ]}
              onPress={() => setStatusFilter(status)}
            >
              <Text style={[
                styles.filterChipText,
                { color: statusFilter === status ? '#ffffff' : textColor }
              ]}>
                {status === 'all' ? 'All' : status.charAt(0).toUpperCase() + status.slice(1)}
              </Text>
            </TouchableOpacity>
          ))}
        </ScrollView>
      </View>
    </View>
  );

  const renderEmpty = () => (
    <View style={styles.emptyState}>
      <ThemedText style={[styles.emptyText, { color: mutedColor }]}>
        {searchQuery || statusFilter !== 'all' ? 'No matching requests' : 'No requests yet'}
      </ThemedText>
      <ThemedText style={[styles.emptySubtext, { color: mutedColor }]}>
        {searchQuery || statusFilter !== 'all' ? 'Try adjusting your filters' : 'Your laundry requests will appear here'}
      </ThemedText>
    </View>
  );

  const renderFooter = () => {
    if (isLoadingMore) {
      return (
        <View style={styles.loadingMore}>
          <ActivityIndicator size="small" color={primaryColor} />
          <ThemedText style={[styles.loadingText, { color: mutedColor }]}>Loading more...</ThemedText>
        </View>
      );
    }

    if (hasMore) {
      return (
        <TouchableOpacity
          style={[styles.loadMoreButton, { backgroundColor: cardColor, borderColor: borderColor }]}
          onPress={loadMore}
        >
          <ThemedText style={[styles.loadMoreText, { color: primaryColor }]}>Load More</ThemedText>
        </TouchableOpacity>
      );
    }

    if (displayedRequests.length > 0) {
      return (
        <View style={styles.endMessage}>
          <ThemedText style={[styles.endMessageText, { color: mutedColor }]}>
            End of results
          </ThemedText>
        </View>
      );
    }

    return null;
  };

  return (
    <ThemedView style={styles.container}>
      <FlatList
        data={displayedRequests}
        renderItem={renderRequestCard}
        keyExtractor={(item) => item.id.toString()}
        ListHeaderComponent={renderHeader}
        ListEmptyComponent={renderEmpty}
        ListFooterComponent={renderFooter}
        contentContainerStyle={styles.listContent}
        refreshControl={
          <RefreshControl refreshing={isLoading} onRefresh={loadRequests} />
        }
        initialNumToRender={10}
        maxToRenderPerBatch={10}
        windowSize={10}
        removeClippedSubviews={true}
      />
      <AlertComponent />
    </ThemedView>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  listContent: {
    flexGrow: 1,
  },
  header: {
    padding: 24,
    paddingTop: 60,
  },
  title: {
    fontSize: 28,
    fontWeight: 'bold',
    marginBottom: 4,
  },
  subtitle: {
    fontSize: 16,
  },
  emptyState: {
    alignItems: 'center',
    padding: 48,
  },
  emptyText: {
    fontSize: 18,
    fontWeight: '500',
  },
  emptySubtext: {
    fontSize: 14,
    textAlign: 'center',
    marginTop: 8,
  },
  requestCard: {
    marginHorizontal: 16,
    marginVertical: 8,
    borderRadius: 12,
    padding: 16,
    borderWidth: 1,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.1,
    shadowRadius: 4,
    elevation: 3,
  },
  requestHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'flex-start',
    marginBottom: 16,
  },
  requestId: {
    fontSize: 18,
    fontWeight: '600',
  },
  requestType: {
    fontSize: 14,
    marginTop: 2,
  },
  statusBadge: {
    paddingHorizontal: 10,
    paddingVertical: 6,
    borderRadius: 16,
  },
  statusText: {
    color: '#ffffff',
    fontSize: 12,
    fontWeight: '600',
  },
  requestDetails: {
    gap: 8,
    marginBottom: 16,
  },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  detailLabel: {
    fontSize: 14,
  },
  detailValue: {
    fontSize: 14,
    fontWeight: '500',
  },
  declineReason: {
    marginTop: 8,
    padding: 12,
    borderRadius: 8,
    borderLeftWidth: 4,
  },
  declineLabel: {
    fontSize: 12,
    fontWeight: '600',
    marginBottom: 4,
  },
  declineText: {
    fontSize: 14,
  },
  trackButton: {
    borderRadius: 8,
    padding: 12,
    alignItems: 'center',
    borderWidth: 1,
  },
  trackButtonText: {
    fontSize: 14,
    fontWeight: '600',
  },
  searchContainer: {
    marginTop: 16,
    borderRadius: 8,
    borderWidth: 1,
    paddingHorizontal: 12,
  },
  searchInput: {
    paddingVertical: 12,
    fontSize: 16,
  },
  filterChipsContainer: {
    marginTop: 12,
  },
  filterLabel: {
    fontSize: 14,
    fontWeight: '600',
    marginBottom: 8,
  },
  filterChipsScroll: {
    flexGrow: 0,
  },
  filterChip: {
    paddingHorizontal: 16,
    paddingVertical: 8,
    borderRadius: 20,
    borderWidth: 1,
    marginRight: 8,
  },
  filterChipText: {
    fontSize: 14,
    fontWeight: '500',
  },
  loadingMore: {
    padding: 20,
    alignItems: 'center',
    flexDirection: 'row',
    justifyContent: 'center',
    gap: 8,
  },
  loadingText: {
    fontSize: 14,
  },
  loadMoreButton: {
    margin: 16,
    marginTop: 8,
    padding: 16,
    borderRadius: 8,
    borderWidth: 1,
    alignItems: 'center',
  },
  loadMoreText: {
    fontSize: 16,
    fontWeight: '600',
  },
  endMessage: {
    padding: 24,
    alignItems: 'center',
  },
  endMessageText: {
    fontSize: 14,
  },
});