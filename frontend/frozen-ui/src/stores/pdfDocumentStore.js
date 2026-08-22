import { create } from 'zustand'

/**
 * Global PDF viewer modal — opened from invoicePdfActions and pages.
 * @typedef {'view'|'download'|'print'} PdfDocumentMode
 */

export const usePdfDocumentStore = create((set) => ({
  isOpen: false,
  title: 'Document',
  filename: 'document.pdf',
  fetchPdf: null,
  directUrl: null,
  mode: 'view',

  openPdfDocument: ({
    title = 'Document',
    filename = 'document.pdf',
    fetchPdf,
    directUrl = null,
    mode = 'view'
  }) => {
    if (!fetchPdf && !directUrl) return
    set({
      isOpen: true,
      title,
      filename,
      fetchPdf,
      directUrl,
      mode
    })
  },

  closePdfDocument: () => set({
    isOpen: false,
    fetchPdf: null,
    directUrl: null,
    mode: 'view'
  })
}))
