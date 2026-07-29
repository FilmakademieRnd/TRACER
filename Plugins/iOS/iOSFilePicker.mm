//
// iOSFilePicker.mm
// Native iOS file picker plugin for Unity
//

#import <UIKit/UIKit.h>
#import <UniformTypeIdentifiers/UniformTypeIdentifiers.h>

// Unity's view controller function
extern UIViewController* UnityGetGLViewController();

@interface iOSFilePickerDelegate : UIViewController <UIDocumentPickerDelegate>
@property (nonatomic, copy) void (^completionHandler)(NSString *);
@end

@implementation iOSFilePickerDelegate

- (void)documentPicker:(UIDocumentPickerViewController *)controller didPickDocumentsAtURLs:(NSArray<NSURL *> *)urls {
    if (urls.count > 0) {
        NSURL *selectedURL = urls[0];

        // Start accessing the security-scoped resource
        if ([selectedURL startAccessingSecurityScopedResource]) {
            NSString *path = [selectedURL path];

            // Copy the file to a temporary location in the app's documents directory
            NSFileManager *fileManager = [NSFileManager defaultManager];
            NSString *documentsPath = [NSSearchPathForDirectoriesInDomains(NSDocumentDirectory, NSUserDomainMask, YES) firstObject];
            NSString *fileName = [selectedURL lastPathComponent];
            NSString *destinationPath = [documentsPath stringByAppendingPathComponent:fileName];

            // Remove existing file if it exists
            if ([fileManager fileExistsAtPath:destinationPath]) {
                [fileManager removeItemAtPath:destinationPath error:nil];
            }

            // Copy the file
            NSError *error = nil;
            if ([fileManager copyItemAtPath:path toPath:destinationPath error:&error]) {
                if (self.completionHandler) {
                    self.completionHandler(destinationPath);
                }
            } else {
                NSLog(@"[iOSFilePicker] Error copying file: %@", error.localizedDescription);
                if (self.completionHandler) {
                    self.completionHandler(@"");
                }
            }

            // Stop accessing the security-scoped resource
            [selectedURL stopAccessingSecurityScopedResource];
        } else {
            if (self.completionHandler) {
                self.completionHandler(@"");
            }
        }
    } else {
        if (self.completionHandler) {
            self.completionHandler(@"");
        }
    }
}

- (void)documentPickerWasCancelled:(UIDocumentPickerViewController *)controller {
    if (self.completionHandler) {
        self.completionHandler(@"");
    }
}

@end

static iOSFilePickerDelegate *filePickerDelegate = nil;
static char* lastSelectedPath = NULL;

extern "C" {
    typedef void (*FilePickerCallback)(const char* path);
    static FilePickerCallback filePickerCallback = NULL;

    void _ShowIOSFilePicker(const char* fileTypes, FilePickerCallback callback) {
        filePickerCallback = callback;

        dispatch_async(dispatch_get_main_queue(), ^{
            // Get Unity's view controller
            UIViewController *rootViewController = UnityGetGLViewController();

            if (rootViewController == nil) {
                NSLog(@"[iOSFilePicker] ERROR: Could not get Unity view controller!");
                if (filePickerCallback != NULL) {
                    filePickerCallback("");
                }
                return;
            }

            // Create document picker
            NSMutableArray *documentTypes = [NSMutableArray array];

            // Parse file types (comma-separated list like "gltf,glb")
            NSString *typesString = [NSString stringWithUTF8String:fileTypes];
            NSArray *types = [typesString componentsSeparatedByString:@","];

            for (NSString *type in types) {
                NSString *trimmedType = [type stringByTrimmingCharactersInSet:[NSCharacterSet whitespaceCharacterSet]];

                // Add UTType for the file extension
                if (@available(iOS 14.0, *)) {
                    if ([trimmedType isEqualToString:@"gltf"] || [trimmedType isEqualToString:@"glb"]) {
                        // For glTF files, use public.data as UTType (or public.item)
                        UTType *dataType = [UTType typeWithFilenameExtension:trimmedType];
                        if (dataType) {
                            [documentTypes addObject:dataType];
                        } else {
                            [documentTypes addObject:UTTypeData];
                        }
                    }
                } else {
                    // Fallback for iOS < 14
                    [documentTypes addObject:@"public.data"];
                }
            }

            // Fallback to allow all files if no specific types matched
            if (documentTypes.count == 0) {
                if (@available(iOS 14.0, *)) {
                    [documentTypes addObject:UTTypeData];
                } else {
                    [documentTypes addObject:@"public.data"];
                }
            }

            UIDocumentPickerViewController *documentPicker;
            if (@available(iOS 14.0, *)) {
                documentPicker = [[UIDocumentPickerViewController alloc] initForOpeningContentTypes:documentTypes];
            } else {
                // Fallback for iOS < 14
                documentPicker = [[UIDocumentPickerViewController alloc] initWithDocumentTypes:@[@"public.data"] inMode:UIDocumentPickerModeOpen];
            }

            // Create and set delegate
            if (filePickerDelegate == nil) {
                filePickerDelegate = [[iOSFilePickerDelegate alloc] init];
            }

            filePickerDelegate.completionHandler = ^(NSString *path) {
                // Store the path
                if (lastSelectedPath != NULL) {
                    free(lastSelectedPath);
                }
                lastSelectedPath = strdup([path UTF8String]);

                // Call the callback
                if (filePickerCallback != NULL) {
                    filePickerCallback(lastSelectedPath);
                }
            };

            documentPicker.delegate = filePickerDelegate;
            documentPicker.allowsMultipleSelection = NO;

            // Present the document picker
            [rootViewController presentViewController:documentPicker animated:YES completion:nil];
        });
    }
}
