'use client';

import { ToastContainer, toast } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

interface ToastProviderProps {
  children: React.ReactNode;
}

export default function ToastProvider({ children }: ToastProviderProps) {
  return (
    <>
      {children}
      <ToastContainer
        position="top-right"
        autoClose={3000}
        hideProgressBar={false}
        newestOnTop={false}
        closeOnClick
        rtl={false}
        pauseOnFocusLoss
        draggable
        pauseOnHover
        theme="dark"
        style={{
          '--toastify-color-dark': '#1e1e1e',
          '--toastify-color-info': '#0dcaf0',
          '--toastify-color-success': '#198754',
          '--toastify-color-warning': '#ffc107',
          '--toastify-color-error': '#dc3545',
          '--toastify-text-color-light': '#ffffff',
        } as React.CSSProperties}
      />
    </>
  );
}

export { toast };
