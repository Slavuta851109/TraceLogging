public class TraceLogService : ITraceLogService
    {
        private readonly IExternalServiceTraceLogRepository _repository;
        
        public TraceLogService(IExternalServiceTraceLogRepository repository)
        {
            _repository = repository;
        }
        
        public async Task<T> RunWithTraceLogging<T>(
            TraceLogDto logRecord, 
            Expression<Func<Task<T>>> func)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var response = await func.Compile().Invoke();

                logRecord.SetDuration(stopwatch.ElapsedMilliseconds);
                logRecord.SetRequest(GetRequestParameters(func));
                logRecord.SetResponse(response);
                await _repository.Log(logRecord);

                return response;
            }
            catch (Exception ex)
            {
                logRecord.SetDuration(stopwatch.ElapsedMilliseconds);
                logRecord.SetException(ex);
                await _repository.Log(logRecord);

                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async Task RunWithTraceLogging(
            TraceLogDto logRecord, 
            Expression<Func<Task>> func)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                await func.Compile().Invoke();

                logRecord.SetDuration(stopwatch.ElapsedMilliseconds);
                logRecord.SetRequest(GetRequestParameters(func));
                await _repository.Log(logRecord);
            }
            catch (Exception ex)
            {
                logRecord.SetDuration(stopwatch.ElapsedMilliseconds);
                logRecord.SetException(ex);
                await _repository.Log(logRecord);

                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        private object GetRequestParameters<T>(Expression<T> expression)
        {
            var call = expression.Body as MethodCallExpression;
            if (call == null)
            {
                throw new InvalidOperationException("Expression is not a method call.");
            }

            if (!call.Arguments.Any())
            {
                return null;
            }

            var response = new List<object>();
            foreach (Expression argument in call.Arguments)
            {
                var @delegate = Expression.Lambda(argument, expression.Parameters).Compile();
                object parameterValue = @delegate.DynamicInvoke();
                response.Add(parameterValue);
            }

            return response;
        }
    }
