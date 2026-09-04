/** ServerGuard.Shared/Dtos/PagedResult.cs karşılığı. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
}
