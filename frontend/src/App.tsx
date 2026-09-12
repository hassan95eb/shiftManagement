import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom';
import { AuthProvider } from './auth/AuthProvider';
import { ProtectedRoute, PublicOnlyRoute } from './components/RouteGuards';
import { FeedbackProvider } from './components/Feedback';
import { EmployerHomePage, ExpertHomePage } from './pages/HomePages';
import { LoginPage } from './pages/LoginPage';
import { NotFoundPage, ServerErrorPage, SessionExpiredPage } from './pages/StatusPages';
import { ProjectsPage } from './pages/ProjectsPage';
import { ExpertDetailPage, ExpertsPage } from './pages/ExpertsPage';
import { EmployerShiftsPage, OpenShiftsPage } from './pages/ShiftsPage';
import { AvailabilityPage } from './pages/AvailabilityPage';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: 1, refetchOnWindowFocus: false, staleTime: 30_000 },
    mutations: { retry: false },
  },
});

export function AppRoutes() {
  return <Routes>
    <Route path="/" element={<Navigate to="/login" replace />} />
    <Route path="/login" element={<PublicOnlyRoute><LoginPage /></PublicOnlyRoute>} />
    <Route path="/employer" element={<ProtectedRoute role="Employer"><EmployerHomePage /></ProtectedRoute>} />
    <Route path="/employer/projects" element={<ProtectedRoute role="Employer"><ProjectsPage /></ProtectedRoute>} />
    <Route path="/employer/experts" element={<ProtectedRoute role="Employer"><ExpertsPage /></ProtectedRoute>} />
    <Route path="/employer/experts/:id" element={<ProtectedRoute role="Employer"><ExpertDetailPage /></ProtectedRoute>} />
    <Route path="/employer/shifts" element={<ProtectedRoute role="Employer"><EmployerShiftsPage /></ProtectedRoute>} />
    <Route path="/expert" element={<ProtectedRoute role="Expert"><ExpertHomePage /></ProtectedRoute>} />
    <Route path="/expert/shifts" element={<ProtectedRoute role="Expert"><OpenShiftsPage /></ProtectedRoute>} />
    <Route path="/expert/availability" element={<ProtectedRoute role="Expert"><AvailabilityPage /></ProtectedRoute>} />
    <Route path="/session-expired" element={<SessionExpiredPage />} />
    <Route path="/server-error" element={<ServerErrorPage />} />
    <Route path="*" element={<NotFoundPage />} />
  </Routes>;
}

export default function App() {
  return <QueryClientProvider client={queryClient}><BrowserRouter><AuthProvider><FeedbackProvider><AppRoutes /></FeedbackProvider></AuthProvider></BrowserRouter></QueryClientProvider>;
}
